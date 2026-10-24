using System.Globalization;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;

namespace Westalife.Infrastructure.Metering.Readers;

/// <summary>
/// SİMÜLATÖR: Gerçek donanım bağlı değilken Faz 3/4/5 geliştirmesi için deterministik test verisi üretir.
/// - 72 sanal Danfoss SONOMETER 1100 kalorimetre (seri no formatı gerçek Izar Center dışa aktarımıyla uyumlu)
/// - Kış/yaz mevsimsel profil + daire başına deterministik varyasyon (seed = kapı numarası + ay)
/// - FailureRate olasılığıyla bir daire "okunamadı" döner → kısmi başarı akışı test edilir
/// Aynı tarih için aynı sonuç üretilir (determinizm → test edilebilirlik).
/// </summary>
public sealed class SimulatedMeterReader : IMeterReader
{
    public const int SimulatedApartmentCount = 72;
    public const string SerialPrefix = "552";

    private readonly MeteringOptions _options;

    public SimulatedMeterReader(MeteringOptions options)
    {
        _options = options;
    }

    public async Task<MeterReadBatch> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        // M-Bus seri iletişimde her okuma belli süre alır; simülasyonda da gerçekçi gecikme uygulanır.
        await Task.Delay(20, cancellationToken);

        var results = new List<MeterReadResult>(SimulatedApartmentCount);

        for (var doorNumber = 1; doorNumber <= SimulatedApartmentCount; doorNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var serialNumber = BuildSerialNumber(doorNumber);
            var primaryAddress = (byte)(doorNumber * 2 % 250); // gerçek dışa aktarımdaki gibi dağınık adresler

            var failureRoll = RollDeterministic(doorNumber, startedAt, "failure");
            if (failureRoll < _options.Simulation.FailureRate)
            {
                results.Add(MeterReadResult.Failure(
                    serialNumber, primaryAddress,
                    MeterReadErrorCode.Timeout,
                    "Simülasyon: cihaz zaman aşımına uğradı (test senaryosu)."));
                continue;
            }

            var snapshot = BuildSnapshot(doorNumber, serialNumber, primaryAddress, startedAt);
            results.Add(MeterReadResult.Success(snapshot));
        }

        return new MeterReadBatch { StartedAtUtc = startedAt, Results = results };
    }

    /// <summary>Kümülatif sayaç: kurulumdan (2024-01-01) bu yana aylık tüketimlerin toplamı — monotonic artar.</summary>
    private static MeterSnapshot BuildSnapshot(int doorNumber, string serialNumber, byte primaryAddress, DateTimeOffset nowUtc)
    {
        var installation = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var averageMonthlyKwh = 850m;

        var cumulative = 0m;
        var month = installation;
        while (month <= nowUtc)
        {
            cumulative += MonthlyConsumption(doorNumber, month, averageMonthlyKwh);
            month = month.AddMonths(1);
        }

        // Kümülatifi tam ay sınırına denk getir; anlık değerler mevsime göre türetilir.
        var seasonalFactor = SeasonalFactor(nowUtc);
        var powerKw = decimal.Round(cumulative % 40m * 0.05m * seasonalFactor, 3);
        var flowM3h = decimal.Round(powerKw / 50m, 3);

        return new MeterSnapshot
        {
            SerialNumber = serialNumber,
            PrimaryAddress = primaryAddress,
            ReadingTimestampUtc = nowUtc,
            CumulativeEnergyKWh = decimal.Round(cumulative, 0),
            CumulativeVolumeM3 = decimal.Round(cumulative * 0.25m, 3),
            VolumeFlowM3PerHour = flowM3h,
            PowerKw = powerKw,
            FlowTemperatureC = decimal.Round(55m + SeasonalFactor(nowUtc) * 5m + (doorNumber % 5) * 0.3m, 1),
            ReturnTemperatureC = decimal.Round(30m + SeasonalFactor(nowUtc) * 8m + (doorNumber % 3) * 0.2m, 1),
            Tariff2EnergyKWh = 0m,
            HasDataError = false,
            RawTelegramHex = null // simülatörde ham telegram yok — gerçek cihazda doldurulur
        };
    }

    /// <summary>Kış yüksek, yaz düşük tüketim (1.0 = kış tepe; ~0.15 = yaz dip).</summary>
    private static decimal SeasonalFactor(DateTimeOffset moment)
    {
        var dayOfYear = moment.DayOfYear;
        var angle = (double)dayOfYear / 365.0 * 2 * Math.PI;
        // Kış tepe noktası ~21 Aralık; yaz dip noktası ~21 Haziran.
        return (decimal)(0.575 - 0.425 * Math.Cos(angle - Math.PI / 12.0));
    }

    /// <summary>Aylık tüketim: mevsimsel profil × daireye özel deterministik varyasyon (±%30).</summary>
    private static decimal MonthlyConsumption(int doorNumber, DateTimeOffset month, decimal averageMonthlyKwh)
    {
        var variation = RollDeterministic(doorNumber, month, "consumption");
        var factor = 0.7m + (variation * 0.6m); // 0.7 .. 1.3
        return decimal.Round(averageMonthlyKwh * SeasonalFactor(month) * factor, 0);
    }

    /// <summary>Deterministik 0..1 "rastgele" değer (seed: daire + ay + amaç).</summary>
    private static decimal RollDeterministic(int doorNumber, DateTimeOffset moment, string purpose)
    {
        var seed = HashCode.Combine(doorNumber, moment.Year * 12 + moment.Month, purpose);
        var random = new Random(seed);
        return (decimal)random.NextDouble();
    }

    private static string BuildSerialNumber(int doorNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"{SerialPrefix}{doorNumber:D5}"); // örn. 55200001
}
