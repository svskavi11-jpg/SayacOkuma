using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Protocol;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Gerçek Izar Center dışa aktarımıyla (14.01.2026 — DAİRE 01: SONOMETER 1100) birebir eşleşen değerlerle
/// kayıt → snapshot eşleme testleri.
/// </summary>
public class DanfossRecordMapperTests
{
    [Fact]
    public void MapToSnapshot_WithRealExportRecordOrder_MapsAllFields()
    {
        var records = new List<MbusDataRecord>
        {
            new() { Dif = 0x00, Vif = 0x00, RawValue = 55238358, RecordIndex = 0 }, // ikincil adres
            new() { Dif = 0x04, Vif = 0x14, RawValue = 19762, RecordIndex = 0 },    // Energy kWh (00)
            new() { Dif = 0x44, Vif = 0x13, RawValue = 2919713, RecordIndex = 1 },  // Volume m³ (01) → 2919.713
            new() { Dif = 0x04, Vif = 0x3B, RawValue = 46, RecordIndex = 2 },       // Flow m³/h (02) → 0.046
            new() { Dif = 0x04, Vif = 0x2C, RawValue = 1586, RecordIndex = 3 },     // Power kW (03) → 1.586
            new() { Dif = 0x04, Vif = 0x5A, RawValue = 572, RecordIndex = 4 },      // Flow Temp (04) → 57.2
            new() { Dif = 0x04, Vif = 0x5A, RawValue = 274, RecordIndex = 5 },      // Return Temp (05) → 27.4
            new() { Dif = 0x04, Vif = 0x14, RawValue = 0, RecordIndex = 1 },        // Tarife-2 Energy (06)
            new() { Dif = 0x02, Vif = 0x22, RawValue = 0, RecordIndex = 6 },        // Op Time (07) → hata yok
        };

        var now = DateTimeOffset.UtcNow;
        var snapshot = DanfossRecordMapper.MapToSnapshot(records, "AABBCCDD", now);

        Assert.Equal("55238358", snapshot.SerialNumber);
        Assert.Equal(19762m, snapshot.CumulativeEnergyKWh);
        Assert.Equal(2919.713m, snapshot.CumulativeVolumeM3);
        Assert.Equal(0.046m, snapshot.VolumeFlowM3PerHour);
        Assert.Equal(1.586m, snapshot.PowerKw);
        Assert.Equal(57.2m, snapshot.FlowTemperatureC);
        Assert.Equal(27.4m, snapshot.ReturnTemperatureC);
        Assert.Equal(0m, snapshot.Tariff2EnergyKWh);
        Assert.False(snapshot.HasDataError);
        Assert.Equal("AABBCCDD", snapshot.RawTelegramHex);
        Assert.Equal(now, snapshot.ReadingTimestampUtc);
    }

    [Fact]
    public void MapToSnapshot_WithNonZeroOperatingTime_SetsDataErrorFlag()
    {
        // Gerçek dışa aktarımdaki DAİRE 12 durumu: kayıt 07 → 0002 (Data Error)
        var records = new List<MbusDataRecord>
        {
            new() { Dif = 0x04, Vif = 0x14, RawValue = 46880, RecordIndex = 0 },
            new() { Dif = 0x02, Vif = 0x22, RawValue = 2, RecordIndex = 6 },
        };

        var snapshot = DanfossRecordMapper.MapToSnapshot(records, string.Empty, DateTimeOffset.UtcNow);

        Assert.True(snapshot.HasDataError);
    }

    [Fact]
    public void MapToSnapshot_WithoutSerialRecord_ReturnsEmptySerialButValidSnapshot()
    {
        var records = new List<MbusDataRecord>
        {
            new() { Dif = 0x04, Vif = 0x14, RawValue = 1000, RecordIndex = 0 },
        };

        var snapshot = DanfossRecordMapper.MapToSnapshot(records, string.Empty, DateTimeOffset.UtcNow);

        Assert.Equal(string.Empty, snapshot.SerialNumber);
        Assert.Equal(1000m, snapshot.CumulativeEnergyKWh);
        Assert.True(snapshot is { CumulativeEnergyKWh: > 0 }); // veri yine de kullanılabilir
    }
}
