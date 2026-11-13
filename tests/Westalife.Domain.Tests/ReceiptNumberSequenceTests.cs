using Westalife.Domain.Services;
using Xunit;

namespace Westalife.Domain.Tests;

public class ReceiptNumberSequenceTests
{
    [Fact]
    public void Next_StartsAt0001()
    {
        var number = ReceiptNumberSequence.Next([], 2026);
        Assert.Equal("2026-0001", number);
    }

    [Fact]
    public void Next_ContinuesTheHighestSuffix()
    {
        var number = ReceiptNumberSequence.Next(["2026-0001", "2026-0009", "2025-0099"], 2026);
        Assert.Equal("2026-0010", number);
    }

    [Fact]
    public void Next_IgnoresManualText()
    {
        var number = ReceiptNumberSequence.Next(["2026-EL", "2026-0002"], 2026);
        Assert.Equal("2026-0003", number);
    }
}
