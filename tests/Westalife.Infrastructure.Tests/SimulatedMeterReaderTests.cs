using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Readers;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class SimulatedMeterReaderTests
{
    private static MeterReadBatch ReadBatch() =>
        new SimulatedMeterReader(new MeteringOptions()).ReadAllAsync().GetAwaiter().GetResult();

    [Fact]
    public void ReadAllAsync_ReturnsResultsForAllApartments()
    {
        var batch = ReadBatch();

        Assert.Equal(SimulatedMeterReader.SimulatedApartmentCount, batch.Results.Count);
        Assert.True(batch.SuccessCount > 0);
    }

    [Fact]
    public void ReadAllAsync_IsDeterministic_ForSameMonth()
    {
        var first = ReadBatch();
        var second = ReadBatch();

        // Aynı ay içinde iki okuma aynı dağılımı üretmelidir (test edilebilirlik).
        Assert.Equal(first.SuccessCount, second.SuccessCount);
        Assert.Equal(first.FailureCount, second.FailureCount);

        for (var i = 0; i < first.Results.Count; i++)
        {
            var a = first.Results[i];
            var b = second.Results[i];
            Assert.Equal(a.IsSuccess, b.IsSuccess);
            if (a.IsSuccess)
            {
                Assert.Equal(a.Snapshot!.CumulativeEnergyKWh, b.Snapshot!.CumulativeEnergyKWh);
                Assert.Equal(a.Snapshot!.SerialNumber, b.Snapshot!.SerialNumber);
            }
        }
    }

    [Fact]
    public void ReadAllAsync_ProducesRealisticSerialNumbers()
    {
        var batch = ReadBatch();

        var successful = batch.Results.Where(r => r.IsSuccess).Select(r => r.Snapshot!).ToList();

        Assert.All(successful, s =>
            Assert.Matches(@"^552\d{5}$", s.SerialNumber));
    }

    [Fact]
    public void ReadAllAsync_CumulativeEnergy_IsPositiveForAllSuccessfulReads()
    {
        var batch = ReadBatch();

        Assert.All(
            batch.Results.Where(r => r.IsSuccess).Select(r => r.Snapshot!),
            s => Assert.True(s.CumulativeEnergyKWh > 0));
    }

    [Fact]
    public void ReadAllAsync_HasDeterministicFailureRate_ForPartialSuccessTesting()
    {
        var batch = ReadBatch();

        // %5 hata oranı: 72 daire için deterministik bir kısmi başarı senaryosu üretmelidir.
        Assert.Equal(MeterReadErrorCode.Timeout, batch.Results.First(r => !r.IsSuccess).ErrorCode);
        Assert.InRange(batch.FailureCount, 1, 20);
    }
}
