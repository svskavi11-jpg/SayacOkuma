using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Deterministik test fatura donemleri. Rastgele tarih yerine her test kendi SABIT "slot"unu kullanir;
/// slot penceresine denk gelen onceki kosudan kalma test faturalari temizlenir (paylasilan test DB).
/// Tum tarihler gecmiste ve seed m² gecerlilik baslangicindan (2026-01-01) sonradir; bu yuzden
/// DateTime.UtcNow'a bagimli degildir.
/// </summary>
internal static class TestPeriods
{
    private static readonly DateTimeOffset BaseDate = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private const int SlotLengthDays = 10;
    private const int PeriodLengthDays = 3;
    private const int ReadingLeadHours = 48; // testler from-24h'ta da okuma girer

    public static async Task<(DateTimeOffset From, DateTimeOffset To)> FixedFreePeriodAsync(
        IApplicationDbContext db, int slot)
    {
        var from = BaseDate.AddDays(slot * SlotLengthDays);
        var to = from.AddDays(PeriodLengthDays);
        var scanStart = from.AddHours(-ReadingLeadHours);

        var staleIds = await db.GasInvoices
            .Where(i => i.ReadingDateFrom < to && scanStart < i.ReadingDateTo)
            .Select(i => i.Id)
            .ToListAsync();

        if (staleIds.Count > 0)
        {
            await db.ApartmentInvoices.Where(a => staleIds.Contains(a.GasInvoiceId)).ExecuteDeleteAsync();
            await db.GasInvoices.Where(i => staleIds.Contains(i.Id)).ExecuteDeleteAsync();
        }

        return (from, to);
    }
}
