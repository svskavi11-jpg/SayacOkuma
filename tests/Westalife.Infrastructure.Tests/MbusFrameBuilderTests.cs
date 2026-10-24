using Westalife.Infrastructure.Metering.Protocol;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>Golden frame testleri: EN 13757 frame üretimi ve checksum doğrulaması.</summary>
public class MbusFrameBuilderTests
{
    [Fact]
    public void BuildSndNke_ForAddress58_ProducesExactGoldenFrame()
    {
        // Adres 58 (gerçek dışa aktarımdaki DAİRE 01'in birincil adresi)
        var frame = MbusFrameBuilder.BuildSndNke(58);

        Assert.Equal(new byte[] { 0x10, 0x40, 0x3A, 0x7A, 0x16 }, frame);
    }

    [Fact]
    public void BuildReqUd2_ForAddress58_ProducesExactGoldenFrame()
    {
        var frame = MbusFrameBuilder.BuildReqUd2(58, frameCountBitSet: true);

        // C=0x7B, A=0x3A, CI=0x7C → checksum = (0x7B+0x3A+0x7C) & 0xFF = 0x31
        Assert.Equal(new byte[] { 0x68, 0x03, 0x03, 0x68, 0x7B, 0x3A, 0x7C, 0x31, 0x16 }, frame);
    }

    [Fact]
    public void BuildReqUd2_WithClearedFcb_UsesFcbClearCode()
    {
        var frame = MbusFrameBuilder.BuildReqUd2(1, frameCountBitSet: false);

        Assert.Equal(0x5B, frame[4]);
        Assert.Equal((byte)(0x5B + 0x01 + 0x7C), frame[7]);
    }
}
