using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Commands.ParseBankStatement;

/// <summary>
/// Banka ekstresi önizleme satır durumu.
/// </summary>
public enum BankStatementRowStatus
{
    /// <summary>Daire, tarih ve tutar geçerli; kayda hazır.</summary>
    Ready = 1,

    /// <summary>Açıklamadan daire numarası bulunamadı; kullanıcının seçmesi gerekiyor.</summary>
    UnmatchedApartment = 2,

    /// <summary>Dekont numarası sistemde (veya dosya içinde) zaten kayıtlı; işleme alınmayacak.</summary>
    Duplicate = 3,

    /// <summary>Tarih veya tutar geçersiz; işleme alınamaz.</summary>
    InvalidRow = 4
}

/// <summary>
/// Önizleme ekranında gösterilecek tek banka ekstresi satırı.
/// </summary>
public sealed record BankStatementPreviewDto(
    int RowNumber,
    DateOnly? PaymentDate,
    string Description,
    string? DocumentNumber,
    decimal Amount,
    Guid? MatchedApartmentId,
    string? MatchedDoorNumber,
    Guid? MatchedResidentId,
    string? MatchedResidentName,
    string? MatchedResidentType,
    LedgerDebtType? SuggestedDebtType,
    BankStatementRowStatus Status,
    string? WarningMessage);

/// <summary>
/// Banka ekstresi ayrıştırma ve akıllı eşleştirme önizleme sonucu (veritabanına yazılmaz).
/// </summary>
public sealed record BankStatementParseResult(
    IReadOnlyList<BankStatementPreviewDto> Rows,
    IReadOnlyList<string> FileErrors,
    int TotalRows,
    int ReadyCount,
    int UnmatchedCount,
    int DuplicateCount,
    int InvalidCount);

public sealed record ParseBankStatementCommand(
    Stream FileStream,
    string FileName) : IRequest<BankStatementParseResult>;

public sealed class ParseBankStatementCommandHandler
    : IRequestHandler<ParseBankStatementCommand, BankStatementParseResult>
{
    private readonly IBankStatementParser _parser;
    private readonly IApplicationDbContext _db;

    public ParseBankStatementCommandHandler(IBankStatementParser parser, IApplicationDbContext db)
    {
        _parser = parser;
        _db = db;
    }

    public async Task<BankStatementParseResult> Handle(
        ParseBankStatementCommand request,
        CancellationToken cancellationToken)
    {
        var parseResult = _parser.Parse(request.FileStream, request.FileName);
        if (parseResult.Rows.Count == 0)
        {
            return new BankStatementParseResult(
                [],
                parseResult.FileErrors,
                TotalRows: 0,
                ReadyCount: 0,
                UnmatchedCount: 0,
                DuplicateCount: 0,
                InvalidCount: 0);
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var apartments = await _db.Apartments
            .Include(a => a.Owners)
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var apartmentByDoor = apartments.ToDictionary(
            a => a.DoorNumber.Trim(),
            a => a,
            StringComparer.OrdinalIgnoreCase);

        var validDoorNumbers = new HashSet<string>(
            apartmentByDoor.Keys,
            StringComparer.OrdinalIgnoreCase);

        var activeResidents = new List<BankStatementMatcher.ResidentLookupItem>();
        foreach (var apt in apartments)
        {
            var kiraci = apt.GetActiveResident(ResidentType.Kiraci, now);
            if (kiraci is not null && !string.IsNullOrWhiteSpace(kiraci.OwnerName))
            {
                activeResidents.Add(new BankStatementMatcher.ResidentLookupItem(
                    apt.Id,
                    apt.DoorNumber.Trim(),
                    kiraci.Id,
                    kiraci.OwnerName,
                    BankStatementMatcher.NormalizeTurkishUpper(kiraci.OwnerName)));
            }

            var evSahibi = apt.GetActiveResident(ResidentType.EvSahibi, now);
            if (evSahibi is not null && !string.IsNullOrWhiteSpace(evSahibi.OwnerName))
            {
                activeResidents.Add(new BankStatementMatcher.ResidentLookupItem(
                    apt.Id,
                    apt.DoorNumber.Trim(),
                    evSahibi.Id,
                    evSahibi.OwnerName,
                    BankStatementMatcher.NormalizeTurkishUpper(evSahibi.OwnerName)));
            }
        }

        // Sistemdeki iptal edilmemiş dekont numaralarını çek (mükerrer kontrolü)
        var candidateDocNumbers = parseResult.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DocumentNumber))
            .Select(r => r.DocumentNumber!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingDocNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (candidateDocNumbers.Count > 0)
        {
            var dbDocs = await _db.Payments
                .AsNoTracking()
                .Where(p => !p.IsCancelled && p.DocumentNumber != null && candidateDocNumbers.Contains(p.DocumentNumber))
                .Select(p => p.DocumentNumber!)
                .ToListAsync(cancellationToken);

            foreach (var doc in dbDocs)
            {
                existingDocNumbers.Add(doc.Trim());
            }
        }

        var seenInFileDocNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewRows = new List<BankStatementPreviewDto>(parseResult.Rows.Count);

        foreach (var raw in parseResult.Rows)
        {
            var docNo = string.IsNullOrWhiteSpace(raw.DocumentNumber) ? null : raw.DocumentNumber.Trim();
            var suggestedDebtType = BankStatementMatcher.InferTargetDebtType(raw.Description);

            // Daire ve sakin eşleştirmesi
            var matchedDoor = BankStatementMatcher.ExtractDoorNumber(
                raw.Description,
                validDoorNumbers,
                activeResidents);

            Guid? matchedApartmentId = null;
            Guid? matchedResidentId = null;
            string? matchedResidentName = null;
            string? matchedResidentType = null;

            if (matchedDoor is not null && apartmentByDoor.TryGetValue(matchedDoor, out var apt))
            {
                matchedApartmentId = apt.Id;
                var resident = apt.ResolveDebtOwner(now);
                if (resident is not null)
                {
                    matchedResidentId = resident.Id;
                    matchedResidentName = resident.OwnerName;
                    matchedResidentType = resident.ResidentType == ResidentType.Kiraci ? "Kiracı" : "Ev Sahibi";
                }
            }

            // Durum ve doğrulama kontrolleri
            BankStatementRowStatus status;
            string? warning = null;

            if (!string.IsNullOrWhiteSpace(raw.ParseError))
            {
                status = BankStatementRowStatus.InvalidRow;
                warning = raw.ParseError;
            }
            else if (raw.PaymentDate is null)
            {
                status = BankStatementRowStatus.InvalidRow;
                warning = $"Geçersiz işlem tarihi (\"{raw.RawDateText}\").";
            }
            else if (raw.PaymentDate.Value > today)
            {
                status = BankStatementRowStatus.InvalidRow;
                warning = DomainConstants.Payment.PaymentDateFuture;
            }
            else if (raw.Amount is null || raw.Amount.Value <= 0m)
            {
                status = BankStatementRowStatus.InvalidRow;
                warning = DomainConstants.Payment.AmountMustBePositive;
            }
            else if (docNo is not null && existingDocNumbers.Contains(docNo))
            {
                status = BankStatementRowStatus.Duplicate;
                warning = $"Mükerrer dekont no ({docNo}): sistemde zaten kayıtlı. İşleme alınmayacak.";
            }
            else if (docNo is not null && !seenInFileDocNumbers.Add(docNo))
            {
                status = BankStatementRowStatus.Duplicate;
                warning = $"Mükerrer dekont no ({docNo}): aynı dosya içinde birden fazla kez geçiyor.";
            }
            else if (matchedApartmentId is null)
            {
                status = BankStatementRowStatus.UnmatchedApartment;
                warning = "Daire numarası açıklamadan çıkarılamadı. Lütfen daire seçin.";
            }
            else
            {
                status = BankStatementRowStatus.Ready;
            }

            previewRows.Add(new BankStatementPreviewDto(
                raw.RowNumber,
                raw.PaymentDate,
                raw.Description,
                docNo,
                raw.Amount ?? 0m,
                matchedApartmentId,
                matchedDoor,
                matchedResidentId,
                matchedResidentName,
                matchedResidentType,
                suggestedDebtType,
                status,
                warning));
        }

        return new BankStatementParseResult(
            previewRows,
            parseResult.FileErrors,
            TotalRows: previewRows.Count,
            ReadyCount: previewRows.Count(r => r.Status == BankStatementRowStatus.Ready),
            UnmatchedCount: previewRows.Count(r => r.Status == BankStatementRowStatus.UnmatchedApartment),
            DuplicateCount: previewRows.Count(r => r.Status == BankStatementRowStatus.Duplicate),
            InvalidCount: previewRows.Count(r => r.Status == BankStatementRowStatus.InvalidRow));
    }
}

