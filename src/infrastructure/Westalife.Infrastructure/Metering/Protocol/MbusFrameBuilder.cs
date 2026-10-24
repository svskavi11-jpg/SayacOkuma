namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>EN 13757 istem frame'lerini üretir (SND_NKE, REQ_UD2).</summary>
public static class MbusFrameBuilder
{
    /// <summary>Kısa frame: 10 C A CS 16</summary>
    public static byte[] BuildShortFrame(byte cField, byte address)
    {
        return
        [
            MbusConstants.FrameStartShort,
            cField,
            address,
            (byte)((cField + address) & 0xFF),
            MbusConstants.FrameStop
        ];
    }

    /// <summary>SND_NKE — sayacın FCB'sini resetler (okuma öncesi standart adım).</summary>
    public static byte[] BuildSndNke(byte address) =>
        BuildShortFrame(MbusConstants.CFieldSndNke, address);

    /// <summary>
    /// REQ_UD2 — değişken veri yapısı talebi. Uzun frame: 68 03 03 68 C A 7C CS 16
    /// </summary>
    public static byte[] BuildReqUd2(byte address, bool frameCountBitSet = true)
    {
        var cField = frameCountBitSet
            ? MbusConstants.CFieldReqUd2FcbSet
            : MbusConstants.CFieldReqUd2FcbClear;

        var frame = new byte[9];
        frame[0] = MbusConstants.FrameStartLong;
        frame[1] = 0x03; // L: C + A + CI
        frame[2] = 0x03;
        frame[3] = MbusConstants.FrameStartLong;
        frame[4] = cField;
        frame[5] = address;
        frame[6] = MbusConstants.CiFieldDataSend;
        frame[7] = (byte)((cField + address + MbusConstants.CiFieldDataSend) & 0xFF);
        frame[8] = MbusConstants.FrameStop;
        return frame;
    }
}
