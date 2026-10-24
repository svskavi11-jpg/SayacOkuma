using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Protocol;

namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>
/// M-Bus veri kayıtlarını <see cref="MeterSnapshot"/>'a eşler.
/// Öncelik VIF tabanlı eşlemedir; VIF tanınmazsa gerçek Izar Center dışa aktarımındaki
/// kayıt sırası (00–09) yedek eşleme olarak kullanılır:
///   00 Energy kWh · 01 Volume m³ · 02 Volume Flow m³/h · 03 Power kW
///   04 Flow Temp °C · 05 Return Temp °C · 06 Tarife-2 Energy · 07 Operating Time (Data Error)
/// Son sıralar hacim tekrarlarıdır; yedek eşlemede yok sayılır.
/// </summary>
public static class DanfossRecordMapper
{
    // VIF sabitleri (Danfoss SONOMETER 1100 — gerçek Izar Center dışa aktarımıyla doğrulanmış ölçekler)
    private const byte VifEnergyKWh = 0x14;        // 1 kWh
    private const byte VifVolumeM3 = 0x13;         // 0.001 m³ (3 ondalık)
    private const byte VifVolumeFlowM3h = 0x3B;    // 0.001 m³/h
    private const byte VifPowerKw = 0x2C;          // 0.001 kW
    private const byte VifTemperatureC = 0x5A;     // 0.1 °C
    private const byte VifOperatingTimeHours = 0x22; // 1 saat (Data Error bayrağı kaynağı)

    private const decimal VolumeScale = 0.001m;
    private const decimal TemperatureScale = 0.1m;

    public static MeterSnapshot MapToSnapshot(
        IReadOnlyList<MbusDataRecord> records, string rawHex, DateTimeOffset readingTimestampUtc)
    {
        var serialNumber = ExtractSerialNumber(records);
        var snapshot = new MeterSnapshot
        {
            SerialNumber = serialNumber,
            ReadingTimestampUtc = readingTimestampUtc,
            RawTelegramHex = rawHex
        };

        // İlk-görülen kayıt mantığı: telegramda seri no kaydı veri kayıtlarından önce gelebildiği
        // için kayıt SIRASI güvenilir değildir; VIF + "ilk görülen" kombinasyonu kullanılır.
        var energySeen = false;
        var temperatureSeen = false;

        foreach (var record in records)
        {
            var value = (decimal)record.RawValue;

            snapshot = record.Vif switch
            {
                VifEnergyKWh when !energySeen => MarkEnergySeen(ref energySeen, snapshot with { CumulativeEnergyKWh = value }),
                VifEnergyKWh => snapshot with { Tariff2EnergyKWh = value },
                VifVolumeM3 => snapshot with { CumulativeVolumeM3 = value * VolumeScale },
                VifVolumeFlowM3h => snapshot with { VolumeFlowM3PerHour = value * VolumeScale },
                VifPowerKw => snapshot with { PowerKw = value * VolumeScale },
                VifTemperatureC when !temperatureSeen => MarkTemperatureSeen(ref temperatureSeen, snapshot with { FlowTemperatureC = value * TemperatureScale }),
                VifTemperatureC => snapshot with { ReturnTemperatureC = value * TemperatureScale },
                VifOperatingTimeHours => snapshot with { HasDataError = record.RawValue != 0 },
                _ => snapshot // tanınmayan VIF — ham telegram zaten taşınıyor, ileride adapte edilebilir
            };
        }

        return snapshot;
    }

    private static MeterSnapshot MarkEnergySeen(ref bool energySeen, MeterSnapshot snapshot)
    {
        energySeen = true;
        return snapshot;
    }

    private static MeterSnapshot MarkTemperatureSeen(ref bool temperatureSeen, MeterSnapshot snapshot)
    {
        temperatureSeen = true;
        return snapshot;
    }

    private static string ExtractSerialNumber(IReadOnlyList<MbusDataRecord> records)
    {
        // İkincil adres kaydı (DIF boyut kodu 0, 8 bayt BCD) — varsa BCD'den okunur.
        var serialRecord = records.FirstOrDefault(r => (r.Dif & 0x0F) == 0);
        if (serialRecord is not null && serialRecord.RawValue > 0)
        {
            return serialRecord.RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }
}
