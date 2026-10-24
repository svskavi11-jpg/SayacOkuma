namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>EN 13757 M-Bus frame sabitleri. Magic byte yasağına karşı tek kaynak.</summary>
public static class MbusConstants
{
    public const byte FrameStartShort = 0x10;
    public const byte FrameStartLong = 0x68;
    public const byte FrameStop = 0x16;
    public const byte SingleCharAck = 0xE5;

    /// <summary>Master → slave: adres resetleme (SND_NKE).</summary>
    public const byte CFieldSndNke = 0x40;

    /// <summary>Master → slave: veri talebi (REQ_UD2), FCB=1 (ilk istem).</summary>
    public const byte CFieldReqUd2FcbSet = 0x7B;

    /// <summary>Master → slave: veri talebi (REQ_UD2), FCB=0 (tekrar istem).</summary>
    public const byte CFieldReqUd2FcbClear = 0x5B;

    /// <summary>Slave → master: veri yanıtı (RSP_UD).</summary>
    public const byte CFieldRspUd = 0x08;

    /// <summary>CI: değişken uzunluklu veri yapısı gönderimi (variable data structure).</summary>
    public const byte CiFieldDataSend = 0x7C;

    /// <summary>Uzun frame'de sabit kısım: 68 L L 68 C A CI (checksum ve stop hariç).</summary>
    public const int LongFrameFixedHeaderLength = 7;
}
