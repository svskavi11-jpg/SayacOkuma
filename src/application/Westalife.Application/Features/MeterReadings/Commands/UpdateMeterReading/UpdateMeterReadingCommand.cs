using FluentValidation;
using MediatR;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.MeterReadings.Commands.UpdateMeterReading;

/// <summary>
/// Okuma düzeltme (Etap 6.7 — tek veya toplu). Her düzeltmede: zincir bütünlüğü kontrol edilir,
/// tüketimler yeniden hesaplanır (sonraki okumanın tüketimi de zincirleme güncellenir) ve
/// zorunlu "Değişiklik Nedeni" ile denetim kaydı yazılır. Kilitli dönem REDDEDİLİR.
/// Kısmi başarı modeli: hatalı satırlar raporlanır, geçerli olanlar kaydedilir.
/// </summary>
public sealed record MeterReadingCorrectionItem(
    Guid ReadingId,
    decimal NewIndexValue,
    string Reason,
    string? ChangedBy);

public sealed record UpdateMeterReadingCommand(IReadOnlyList<MeterReadingCorrectionItem> Items)
    : IRequest<UpdateMeterReadingResult>;

public sealed record UpdateMeterReadingResult(
    int UpdatedCount,
    IReadOnlyList<string> RowErrors);

public sealed class UpdateMeterReadingCommandValidator : AbstractValidator<UpdateMeterReadingCommand>
{
    public UpdateMeterReadingCommandValidator()
    {
        RuleFor(c => c.Items)
            .NotEmpty()
            .WithMessage("Düzeltilecek en az bir okuma seçilmelidir.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ReadingId).NotEmpty();
            item.RuleFor(x => x.NewIndexValue)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative);
            item.RuleFor(x => x.Reason)
                .NotEmpty()
                .MaximumLength(DomainConstants.MeterReadingAuditLog.ReasonMaxLength)
                .WithMessage(DomainConstants.ValidationMessages.ReadingCorrectionReasonRequired);
            item.RuleFor(x => x.ChangedBy)
                .MaximumLength(DomainConstants.MeterReadingAuditLog.ChangedByMaxLength);
        });
    }
}

/// <summary>Kilitli fatura dönemi penceresi (satır bazlı engelleme).</summary>
public sealed record LockedPeriodWindow(DateTimeOffset ReadingDateFrom, DateTimeOffset ReadingDateTo);