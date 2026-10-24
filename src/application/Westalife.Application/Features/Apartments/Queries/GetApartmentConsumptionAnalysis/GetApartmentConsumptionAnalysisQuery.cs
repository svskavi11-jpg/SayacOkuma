using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Queries.GetApartmentConsumptionAnalysis;

/// <summary>
/// Daire tüketim analizi (Etap 6.7): yıl VEYA tarih aralığı filtreli aylık tüketim serisi.
/// Tüketim, okuma kayıtlarındaki dönem tüketimlerinden (ConsumptionKWh) aylık kovalara dağıtılır;
/// aralıktaki veri olmayan aylar 0 ile doldurulur (grafik sürekliliği).
/// </summary>
public sealed record GetApartmentConsumptionAnalysisQuery(
    Guid ApartmentId,
    int? Year,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<ApartmentConsumptionAnalysisResponse>;

public sealed record ApartmentConsumptionAnalysisResponse(
    Guid ApartmentId,
    string DoorNumber,
    string Title,
    int? Year,
    DateTimeOffset? From,
    DateTimeOffset? To,
    decimal TotalConsumptionKWh,
    decimal AverageMonthlyConsumptionKWh,
    int ReadingCount,
    IReadOnlyList<MonthlyConsumptionItem> Monthly,
    IReadOnlyList<AnalysisReadingItem> Readings);

public sealed record MonthlyConsumptionItem(
    int Year, int Month, string MonthName, decimal ConsumptionKWh, int ReadingCount);

public sealed record AnalysisReadingItem(
    Guid ReadingId,
    DateTimeOffset ReadingDateUtc,
    decimal IndexValue,
    decimal ConsumptionKWh,
    string Source,
    bool IsInitialReading);

public sealed class GetApartmentConsumptionAnalysisQueryHandler
    : IRequestHandler<GetApartmentConsumptionAnalysisQuery, ApartmentConsumptionAnalysisResponse>
{
    private readonly IApplicationDbContext _db;

    public GetApartmentConsumptionAnalysisQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ApartmentConsumptionAnalysisResponse> Handle(
        GetApartmentConsumptionAnalysisQuery request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new DomainException(DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber);

        // Dairenin aktif kalorimetresi (ImportReadingsFromExcel ile aynı deterministik seçim).
        var meterId = await _db.Meters
            .Where(m => m.IsActive && m.Type == MeterType.Calorimeter && m.ApartmentId == apartment.Id)
            .OrderByDescending(m => m.CreatedOn)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Tarih aralığı çözümleme: RangePicker > yıl > varsayılan (bu yıl).
        var culture = new System.Globalization.CultureInfo("tr-TR");
        DateTimeOffset from;
        DateTimeOffset to;
        int? year = request.Year;

        if (request.From.HasValue && request.To.HasValue && request.From.Value < request.To.Value)
        {
            from = request.From.Value;
            to = request.To.Value;
            year = null; // aralık seçildiyse yıl filtresi uygulanmaz
        }
        else
        {
            var y = request.Year ?? DateTimeOffset.UtcNow.Year;
            year = y;
            from = new DateTimeOffset(y, 1, 1, 0, 0, 0, TimeSpan.Zero);
            to = from.AddYears(1);
        }

        var readings = meterId is null
            ? []
            : await _db.MeterReadings
                .AsNoTracking()
                .Where(r => r.MeterId == meterId.Value
                    && r.ReadingDateUtc >= from && r.ReadingDateUtc < to)
                .OrderBy(r => r.ReadingDateUtc)
                .Select(r => new AnalysisReadingItem(
                    r.Id, r.ReadingDateUtc, r.IndexValue, r.ConsumptionKWh,
                    r.Source.ToString(), r.IsInitialReading))
                .ToListAsync(cancellationToken);

        // Aylık kovalar: aralıktaki TÜM aylar listelenir, veri olmayan ay 0 kalır.
        var monthly = new List<MonthlyConsumptionItem>();
        for (var cursor = new DateTimeOffset(from.Year, from.Month, 1, 0, 0, 0, from.Offset);
             cursor < to;
             cursor = cursor.AddMonths(1))
        {
            var monthReadings = readings
                .Where(r => r.ReadingDateUtc.Year == cursor.Year && r.ReadingDateUtc.Month == cursor.Month)
                .ToList();

            monthly.Add(new MonthlyConsumptionItem(
                cursor.Year,
                cursor.Month,
                culture.DateTimeFormat.GetAbbreviatedMonthName(cursor.Month),
                monthReadings.Sum(r => r.ConsumptionKWh),
                monthReadings.Count));
        }

        var total = readings.Sum(r => r.ConsumptionKWh);
        var monthsWithData = monthly.Count(m => m.ReadingCount > 0);

        return new ApartmentConsumptionAnalysisResponse(
            apartment.Id,
            apartment.DoorNumber,
            apartment.Title,
            year,
            request.From.HasValue && request.To.HasValue && request.From.Value < request.To.Value
                ? request.From
                : null,
            request.From.HasValue && request.To.HasValue && request.From.Value < request.To.Value
                ? request.To
                : null,
            total,
            monthsWithData == 0 ? 0m : Math.Round(total / monthsWithData, 3),
            readings.Count,
            monthly,
            readings);
    }
}