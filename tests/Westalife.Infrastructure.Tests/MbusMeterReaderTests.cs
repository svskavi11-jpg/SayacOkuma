using Microsoft.Extensions.Logging.Abstractions;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Connections;
using Westalife.Infrastructure.Metering.Readers;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Fake kanal ile uçtan uca MbusMeterReader testi: SND_NKE → ACK → REQ_UD2 → RSP_UD akışı,
/// kısmi başarı davranışı ve protokol hatası yönetimi.
/// </summary>
public class MbusMeterReaderTests
{
    private sealed class FakeMbusChannel : IMbusChannel
    {
        private readonly byte[] _rspFrame;
        private readonly bool _failNke;

        public FakeMbusChannel(byte[] rspFrame, bool failNke = false)
        {
            _rspFrame = rspFrame;
            _failNke = failNke;
        }

        public bool IsOpen { get; private set; }

        public bool NkeSent { get; private set; }

        public bool ReqUd2Sent { get; private set; }

        public bool Disposed { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (data.Length == 5 && data[0] == 0x10 && data[1] == 0x40)
            {
                NkeSent = true;
            }
            else if (data[0] == 0x68)
            {
                ReqUd2Sent = true;
            }

            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default)
        {
            if (NkeSent && !ReqUd2Sent)
            {
                if (_failNke)
                {
                    return Task.FromResult<byte[]>([0xFF]); // ACK beklenirken çöp yanıt
                }

                return Task.FromResult<byte[]>([0xE5]);
            }

            return Task.FromResult(_rspFrame);
        }

        public void Dispose() => Disposed = true;
    }

    private static byte[] BuildRealisticRspFrame(byte address)
    {
        // Gerçek dışa aktarımdaki DAİRE 01 değerleriyle RSP_UD:
        // ikincil adres + enerji 19762 kWh + hacim 2919.713 m³
        var data = new byte[]
        {
            0x00, 0x13, 0x58, 0x83, 0x23, 0x55, 0x00, 0x00, 0x00, 0x00, // seri no 55238358
            0x04, 0x14, 0x32, 0x4D, 0x00, 0x00,                          // 19762 kWh
            0x44, 0x13, 0x13, 0x97, 0x91, 0x02,                          // 2919.713 m³
        };

        var length = 3 + data.Length;
        var frame = new byte[length + 6];
        frame[0] = 0x68;
        frame[1] = (byte)length;
        frame[2] = (byte)length;
        frame[3] = 0x68;
        frame[4] = 0x08;
        frame[5] = address;
        frame[6] = 0x7C;
        Array.Copy(data, 0, frame, 7, data.Length);

        var checksum = 0;
        for (var i = 4; i < 7 + data.Length; i++)
        {
            checksum += frame[i];
        }

        frame[^2] = (byte)(checksum & 0xFF);
        frame[^1] = 0x16;
        return frame;
    }

    [Fact]
    public async Task ReadAllAsync_WithHealthyFakeDevice_ReturnsSuccessfulSnapshot()
    {
        var channel = new FakeMbusChannel(BuildRealisticRspFrame(58));
        var reader = new MbusMeterReader(channel, new MeteringOptions(), [58], NullLogger<MbusMeterReader>.Instance);

        var batch = await reader.ReadAllAsync();

        Assert.True(channel.NkeSent);
        Assert.True(channel.ReqUd2Sent);
        Assert.True(channel.Disposed);
        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);

        var snapshot = Assert.Single(batch.Results).Snapshot!;
        Assert.Equal("55238358", snapshot.SerialNumber);
        Assert.Equal(19762m, snapshot.CumulativeEnergyKWh);
        Assert.Equal(2919.713m, snapshot.CumulativeVolumeM3);
        Assert.NotNull(snapshot.RawTelegramHex);
    }

    [Fact]
    public async Task ReadAllAsync_WithGarbageNkeResponse_ReportsProtocolErrorWithoutThrowing()
    {
        var channel = new FakeMbusChannel(BuildRealisticRspFrame(58), failNke: true);
        var reader = new MbusMeterReader(channel, new MeteringOptions(), [58], NullLogger<MbusMeterReader>.Instance);

        var batch = await reader.ReadAllAsync();

        // Kısmi başarı modeli: hata exception olarak dışarı sızmaz, sonuç listesinde raporlanır.
        Assert.Equal(0, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        var failure = Assert.Single(batch.Results);
        Assert.False(failure.IsSuccess);
        Assert.Equal(MeterReadErrorCode.ProtocolError, failure.ErrorCode);
    }

    [Fact]
    public async Task ReadAllAsync_WithEmptyAddressList_ReturnsEmptyBatchWithWarning()
    {
        var channel = new FakeMbusChannel(BuildRealisticRspFrame(1));
        var reader = new MbusMeterReader(channel, new MeteringOptions(), [], NullLogger<MbusMeterReader>.Instance);

        var batch = await reader.ReadAllAsync();

        Assert.Empty(batch.Results);
        Assert.False(channel.NkeSent); // hiçbir istek gönderilmemeli
    }
}
