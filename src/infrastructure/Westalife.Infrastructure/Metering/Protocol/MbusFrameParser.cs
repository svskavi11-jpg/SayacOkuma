using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>
/// M-Bus yanıt frame'lerini doğrular (start/stop, L tutarlılığı, checksum, C-field).
/// Geçersiz frame durumunda MbusProtocolException fırlatır.
/// </summary>
public static class MbusFrameParser
{
    public static bool IsAck(ReadOnlySpan<byte> buffer) =>
        buffer.Length == 1 && buffer[0] == MbusConstants.SingleCharAck;

    public static MbusFrame ParseResponse(byte[] frame)
    {
        ValidateFrame(frame);

        var length = frame[1];
        var dataLength = length - 3; // C + A + CI veri bölümünden düşülür

        var data = new byte[Math.Max(dataLength, 0)];
        if (dataLength > 0)
        {
            Array.Copy(frame, MbusConstants.LongFrameFixedHeaderLength, data, 0, dataLength);
        }

        return new MbusFrame
        {
            CField = frame[4],
            Address = frame[5],
            CiField = frame[6],
            Data = data,
            RawHex = Convert.ToHexString(frame)
        };
    }

    private static void ValidateFrame(byte[] frame)
    {
        if (frame.Length < 9)
        {
            throw new MbusProtocolException("Frame çok kısa: minimum 9 byte beklenir.", Convert.ToHexString(frame));
        }

        if (frame[0] != MbusConstants.FrameStartLong || frame[3] != MbusConstants.FrameStartLong)
        {
            throw new MbusProtocolException("Frame başlangıç baytları geçersiz (0x68 beklenir).", Convert.ToHexString(frame));
        }

        if (frame[^1] != MbusConstants.FrameStop)
        {
            throw new MbusProtocolException("Frame stop baytı geçersiz (0x16 beklenir).", Convert.ToHexString(frame));
        }

        if (frame[1] != frame[2])
        {
            throw new MbusProtocolException("Frame uzunluk alanları (L L) tutarsız.", Convert.ToHexString(frame));
        }

        if (frame.Length != frame[1] + 6)
        {
            throw new MbusProtocolException(
                $"Frame uzunluğu L alanı ile uyuşmuyor (beklenen {frame[1] + 6}, gelen {frame.Length}).",
                Convert.ToHexString(frame));
        }

        if (frame[4] != MbusConstants.CFieldRspUd)
        {
            throw new MbusProtocolException(
                $"Beklenmeyen C-field: 0x{frame[4]:X2} (RSP_UD = 0x08 beklenir).",
                Convert.ToHexString(frame));
        }

        var checksum = CalculateChecksum(frame);
        if (checksum != frame[^2])
        {
            throw new MbusProtocolException(
                $"Checksum hatalı (beklenen 0x{checksum:X2}, gelen 0x{frame[^2]:X2}).",
                Convert.ToHexString(frame));
        }
    }

    /// <summary>C alanından son veri baytına kadar olan baytların toplamı (mod 256).</summary>
    public static byte CalculateChecksum(byte[] frame)
    {
        var length = frame[1];
        var start = MbusConstants.LongFrameFixedHeaderLength - 3; // C alanının indeksi (4)
        var sum = 0;
        for (var i = start; i < start + length; i++)
        {
            sum += frame[i];
        }

        return (byte)(sum & 0xFF);
    }
}
