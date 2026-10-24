using System.Net;
using System.Net.Sockets;
using Westalife.Infrastructure.Metering.Connections;
using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// LAN/Ethernet (TCP) M-Bus kanali: sahte bir Izar Center (TcpListener) ile baglanti, parcali frame,
/// gec gelen yanit (senkron kayma), karsi tarafin baglantiyi kapatmasi ve baglanti reddi senaryolari.
/// Gercek donanim gerektirmez; tum zamanlamalar sabit/deterministiktir.
/// </summary>
public class MbusTcpChannelTests
{
    private const int ClientTimeoutMs = 300;
    private const byte MeterAddress = 5;
    private static readonly byte[] SampleData = [0x04, 0x14, 0x72, 0x4D, 0x00, 0x00];

    private static TcpListener StartListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static async Task ReadFullAsync(NetworkStream stream, byte[] buffer)
    {
        await stream.ReadExactlyAsync(buffer);
    }

    [Fact]
    public async Task SndNke_ReceivesSingleCharAck()
    {
        using var listener = StartListener();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            await ReadFullAsync(stream, new byte[5]); // SND_NKE kisa frame
            await stream.WriteAsync(new byte[] { MbusConstants.SingleCharAck });
        });

        using var channel = new MbusTcpChannel("127.0.0.1", PortOf(listener), ClientTimeoutMs);
        await channel.OpenAsync();
        await channel.SendAsync(MbusFrameBuilder.BuildSndNke(MeterAddress));
        var response = await channel.ReceiveAsync(ClientTimeoutMs);
        await server;

        Assert.True(channel.IsOpen);
        Assert.True(MbusFrameParser.IsAck(response));
    }

    [Fact]
    public async Task ReqUd2_FrameSplitAcrossTcpSegments_IsReassembled()
    {
        using var listener = StartListener();
        var frame = MbusFrameParserTests.BuildRspUd(MeterAddress, SampleData);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            client.NoDelay = true;
            var stream = client.GetStream();
            await ReadFullAsync(stream, new byte[9]); // REQ_UD2 uzun frame
            await stream.WriteAsync(frame.AsMemory(0, 4));
            await stream.FlushAsync();
            await Task.Delay(100); // frame ortasinda gecikme (ag/gateway parcalamasi)
            await stream.WriteAsync(frame.AsMemory(4));
        });

        using var channel = new MbusTcpChannel("127.0.0.1", PortOf(listener), ClientTimeoutMs);
        await channel.OpenAsync();
        await channel.SendAsync(MbusFrameBuilder.BuildReqUd2(MeterAddress));
        var response = await channel.ReceiveAsync(ClientTimeoutMs);
        await server;

        Assert.Equal(frame, response);
        Assert.Equal(SampleData, MbusFrameParser.ParseResponse(response).Data);
    }

    [Fact]
    public async Task NoResponse_ThrowsTimeoutException()
    {
        using var listener = StartListener();
        var release = new TaskCompletionSource();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await release.Task; // hic yanit vermez (cevap vermeyen sayac)
        });

        using var channel = new MbusTcpChannel("127.0.0.1", PortOf(listener), ClientTimeoutMs);
        await channel.OpenAsync();
        await channel.SendAsync(MbusFrameBuilder.BuildSndNke(MeterAddress));

        await Assert.ThrowsAsync<MbusTimeoutException>(() => channel.ReceiveAsync(ClientTimeoutMs));

        release.SetResult();
        await server;
    }

    [Fact]
    public async Task LateResponseFromPreviousRequest_IsDiscarded_NextRequestGetsItsOwnResponse()
    {
        using var listener = StartListener();
        var lateFrame = MbusFrameParserTests.BuildRspUd(MeterAddress, SampleData);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            client.NoDelay = true;
            var stream = client.GetStream();

            // 1. istek: yanit, istemci zaman asimindan SONRA gelir.
            await ReadFullAsync(stream, new byte[9]); // REQ_UD2 uzun frame
            await Task.Delay(ClientTimeoutMs + 200);
            await stream.WriteAsync(lateFrame);

            // 2. istek: aninda ACK.
            await ReadFullAsync(stream, new byte[5]);
            await stream.WriteAsync(new byte[] { MbusConstants.SingleCharAck });
        });

        using var channel = new MbusTcpChannel("127.0.0.1", PortOf(listener), ClientTimeoutMs);
        await channel.OpenAsync();

        await channel.SendAsync(MbusFrameBuilder.BuildReqUd2(MeterAddress));
        await Assert.ThrowsAsync<MbusTimeoutException>(() => channel.ReceiveAsync(ClientTimeoutMs));

        await Task.Delay(ClientTimeoutMs + 400); // gec yanit soket tamponuna ulasir
        await channel.SendAsync(MbusFrameBuilder.BuildSndNke(MeterAddress));
        var response = await channel.ReceiveAsync(ClientTimeoutMs);
        await server;

        Assert.True(MbusFrameParser.IsAck(response), "Eski yanit atilmali; 2. istek kendi ACK'ini almali.");
    }

    [Fact]
    public async Task PeerClosesConnection_ThrowsConnectionException()
    {
        using var listener = StartListener();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await ReadFullAsync(client.GetStream(), new byte[5]);
            // using sonu: baglanti yanit vermeden kapanir.
        });

        using var channel = new MbusTcpChannel("127.0.0.1", PortOf(listener), ClientTimeoutMs);
        await channel.OpenAsync();
        await channel.SendAsync(MbusFrameBuilder.BuildSndNke(MeterAddress));
        await server;

        await Assert.ThrowsAsync<MbusConnectionException>(() => channel.ReceiveAsync(ClientTimeoutMs));
    }

    [Fact]
    public async Task ConnectionRefused_ThrowsConnectionException()
    {
        // Dinleyen yok: portu al, kapat.
        var listener = StartListener();
        var port = PortOf(listener);
        listener.Stop();

        using var channel = new MbusTcpChannel("127.0.0.1", port, ClientTimeoutMs);

        await Assert.ThrowsAsync<MbusConnectionException>(() => channel.OpenAsync());
        Assert.False(channel.IsOpen);
    }
}
