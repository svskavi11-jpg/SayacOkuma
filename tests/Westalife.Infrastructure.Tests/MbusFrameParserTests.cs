using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class MbusFrameParserTests
{
    /// <summary>Geçerli bir RSP_UD uzun frame'i üretir (test yardımcısı).</summary>
    public static byte[] BuildRspUd(byte address, byte[] data)
    {
        var length = 3 + data.Length;
        var frame = new byte[length + 6];
        frame[0] = 0x68;
        frame[1] = (byte)length;
        frame[2] = (byte)length;
        frame[3] = 0x68;
        frame[4] = MbusConstants.CFieldRspUd;
        frame[5] = address;
        frame[6] = MbusConstants.CiFieldDataSend;
        Array.Copy(data, 0, frame, 7, data.Length);

        var checksum = 0;
        for (var i = 4; i < 7 + data.Length; i++)
        {
            checksum += frame[i];
        }

        frame[^2] = (byte)(checksum & 0xFF);
        frame[^1] = MbusConstants.FrameStop;
        return frame;
    }

    [Fact]
    public void ParseResponse_WithValidFrame_ReturnsCFieldAddressAndData()
    {
        var data = new byte[] { 0x04, 0x14, 0x72, 0x4D, 0x00, 0x00 }; // 19762 kWh
        var frame = BuildRspUd(58, data);

        var parsed = MbusFrameParser.ParseResponse(frame);

        Assert.Equal(MbusConstants.CFieldRspUd, parsed.CField);
        Assert.Equal((byte)58, parsed.Address);
        Assert.Equal(MbusConstants.CiFieldDataSend, parsed.CiField);
        Assert.Equal(data, parsed.Data);
        // data 6 bayt → L = 3 + 6 = 9 (0x09); hex başlangıcı: 68 09 09 68 08 3A 7C
        Assert.StartsWith("68090968083A7C", parsed.RawHex);
    }

    [Fact]
    public void ParseResponse_WithCorruptedChecksum_ThrowsProtocolException()
    {
        var frame = BuildRspUd(58, [0x04, 0x14, 0x72, 0x4D, 0x00, 0x00]);
        frame[^2] ^= 0xFF; // checksum bozulur

        Assert.Throws<MbusProtocolException>(() => MbusFrameParser.ParseResponse(frame));
    }

    [Fact]
    public void ParseResponse_WithWrongCField_ThrowsProtocolException()
    {
        var frame = BuildRspUd(58, []);
        frame[4] = 0xFF; // RSP_UD (0x08) değil

        // C alanı değiştiği için checksum da geçersiz olur; her iki durumda da ProtocolException beklenir.
        Assert.Throws<MbusProtocolException>(() => MbusFrameParser.ParseResponse(frame));
    }

    [Fact]
    public void IsAck_WithSingleE5Byte_ReturnsTrue()
    {
        Assert.True(MbusFrameParser.IsAck([0xE5]));
        Assert.False(MbusFrameParser.IsAck([0x08]));
        Assert.False(MbusFrameParser.IsAck([0xE5, 0xE5]));
    }
}
