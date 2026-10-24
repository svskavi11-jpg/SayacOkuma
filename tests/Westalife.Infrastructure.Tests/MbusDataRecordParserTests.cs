using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class MbusDataRecordParserTests
{
    [Fact]
    public void Parse_With32BitEnergyRecord_DecodesLittleEndianValue()
    {
        // 19762 kWh = 0x00004D32 → LE: 32 4D 00 00
        var data = new byte[] { 0x04, 0x14, 0x32, 0x4D, 0x00, 0x00 };

        var records = MbusDataRecordParser.Parse(data);

        var record = Assert.Single(records);
        Assert.Equal(0x04, record.Dif);
        Assert.Equal(0x14, record.Vif);
        Assert.Equal(19762, record.RawValue);
        Assert.Equal(0, record.RecordIndex);
    }

    [Fact]
    public void Parse_WithBcdVolumeRecord_DecodesBcdValue()
    {
        // 2919.713 m³ ham değeri (DIF 0x44 = 4 bayt BCD, little-endian gruplar): 13 97 91 02 → 2919713
        var data = new byte[] { 0x44, 0x13, 0x13, 0x97, 0x91, 0x02 };

        var records = MbusDataRecordParser.Parse(data);

        Assert.Equal(2919713, records[0].RawValue);
    }

    [Fact]
    public void Parse_WithSecondaryAddressRecord_ReturnsBcdSerial()
    {
        // DIF boyut kodu 0 → 8 baytlık ikincil adres; ilk 4 bayt BCD: 58 83 23 55 → 55238358
        var data = new byte[] { 0x00, 0x13, 0x58, 0x83, 0x23, 0x55, 0x00, 0x00, 0x00, 0x00 };

        var records = MbusDataRecordParser.Parse(data);

        Assert.Equal(55238358, records[0].RawValue);
    }

    [Fact]
    public void Parse_WithManufacturerDataTerminator_StopsParsing()
    {
        var data = new byte[] { 0x04, 0x14, 0x72, 0x4D, 0x00, 0x00, 0x0F, 0xFF, 0xFF };

        var records = MbusDataRecordParser.Parse(data);

        Assert.Single(records);
    }

    [Fact]
    public void Parse_WithNegative8BitValue_UsesSignExtension()
    {
        // 8-bit işaretli: -5 → 0xFB
        var data = new byte[] { 0x01, 0x5A, 0xFB };

        var records = MbusDataRecordParser.Parse(data);

        Assert.Equal(-5, records[0].RawValue);
    }
}
