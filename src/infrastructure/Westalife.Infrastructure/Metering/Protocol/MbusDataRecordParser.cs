using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>
/// RSP_UD değişken veri yapısını (EN 13757) DIF/VIF kayıtlarına ayrıştırır.
/// DIF uzantıları (DIFE/VIFE) atlanır; ham değer tam sayı olarak döner, ölçekleme mapper'da yapılır.
/// </summary>
public static class MbusDataRecordParser
{
    public static IReadOnlyList<MbusDataRecord> Parse(byte[] data)
    {
        var records = new List<MbusDataRecord>();
        var index = 0;
        var i = 0;

        while (i < data.Length)
        {
            var dif = data[i++];

            // DIF 0x0F / 0x2F: üreticiye özel veri — bu telegram burada biter.
            if (dif == 0x0F || dif == 0x2F)
            {
                break;
            }

            // DIFE zincirini atla (DIF'in 7. biti uzantı işaretidir).
            while (i < data.Length && (dif & 0x80) != 0)
            {
                // (DIF zaten okundu; burada DIFE baytları ilerler)
                var dife = data[i++];
                if ((dife & 0x80) == 0)
                {
                    break;
                }
            }

            if (i >= data.Length)
            {
                break;
            }

            var vif = data[i++];

            // VIFE zincirini atla.
            while (i < data.Length && (vif & 0x80) != 0)
            {
                var vife = data[i++];
                if ((vife & 0x80) == 0)
                {
                    break;
                }
            }

            if (i >= data.Length)
            {
                break;
            }

            var (bytesToRead, rawValue) = DecodeValue(data, i, dif);
            i += bytesToRead;

            records.Add(new MbusDataRecord
            {
                Dif = dif,
                Vif = vif,
                RawValue = rawValue,
                RecordIndex = index++
            });
        }

        return records;
    }

    private static (int ByteCount, long Value) DecodeValue(byte[] data, int offset, byte dif)
    {
        var isBcd = (dif & 0x40) != 0;
        var sizeCode = (byte)(dif & 0x0F);

        if (sizeCode == 0)
        {
            // Boyut kodu 0: üretim numarası / ikincil adres kaydı (8 bayt BCD benzeri).
            var serial = ReadBcd(data, offset, 4);
            return (8, serial);
        }

        if (isBcd)
        {
            var value = ReadBcd(data, offset, sizeCode);
            return (sizeCode, value);
        }

        switch (sizeCode)
        {
            case 1: return (1, (sbyte)data[offset]);                                  // 8-bit int
            case 2: return (2, BitConverter.ToInt16(data, offset));                   // 16-bit int
            case 3: return (3, ReadInt24(data, offset));                              // 24-bit int
            case 4: return (4, BitConverter.ToInt32(data, offset));                   // 32-bit int
            case 6: return (8, BitConverter.ToInt64(data, offset));                   // 64-bit int
            default:
                throw new MbusProtocolException(
                    $"Desteklenmeyen DIF boyut kodu: 0x{sizeCode:X2} (DIF 0x{dif:X2}).");
        }
    }

    private static long ReadInt24(byte[] data, int offset)
    {
        var value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000); // işaret genişletme
        }

        return value;
    }

    /// <summary>BCD baytlarını (her bayt 2 hane, little-endian hane düzeni) tam sayıya çevirir.</summary>
    private static long ReadBcd(byte[] data, int offset, int byteCount)
    {
        long value = 0;
        var multiplier = 1L;
        for (var i = 0; i < byteCount; i++)
        {
            var b = data[offset + i];
            var low = b & 0x0F;
            var high = (b >> 4) & 0x0F;
            if (low > 9 || high > 9)
            {
                throw new MbusProtocolException($"Geçersiz BCD baytı: 0x{b:X2} (offset {offset + i}).");
            }

            value += (high * 10L + low) * multiplier;
            multiplier *= 100;
        }

        return value;
    }
}
