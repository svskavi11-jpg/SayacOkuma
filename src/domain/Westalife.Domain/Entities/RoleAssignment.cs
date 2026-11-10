using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Daire görev kaydı (SCD Type 2). Kapanan satır silinmez; bitiş tarihi yazılır.
/// Aynı dairede aralıklar çakışamaz. Farklı dairelerin görevleri aynı anda durabilir.
/// Oran (%0–%100) veya Sabit Tutar (Aylık / Yıllık Huzur Hakkı) destekler.
/// </summary>
public class RoleAssignment : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    public string PersonName { get; private set; } = string.Empty;

    public RoleType RoleType { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Boşsa görev açıktır.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>0 ile 1 arası. 0 görev kaydıdır, oran bazlı mahsup üretmez.</summary>
    public decimal ExemptionRate { get; private set; }

    /// <summary>Ücret / muafiyet tipi: Oran (1) veya SabitTutar (2).</summary>
    public RolePaymentType PaymentType { get; private set; } = RolePaymentType.Oran;

    /// <summary>SabitTutar seçildiğinde huzur hakkı / yönetim ücreti tutarı (TL).</summary>
    public decimal? FixedAmount { get; private set; }

    /// <summary>SabitTutar seçildiğinde ücret periyodu: Aylık (1) veya Yıllık (2).</summary>
    public RolePeriodType? PeriodType { get; private set; }

    public DateOnly? BoardDecisionDate { get; private set; }

    /// <summary>Görev ataması iptal edildi mi? (Soft Delete — denetim izi korunur).</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>Görev atamasının iptal edildiği zaman (UTC).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    private RoleAssignment() { }

    public static RoleAssignment Create(
        Guid apartmentId,
        string personName,
        RoleType roleType,
        DateOnly startDate,
        DateOnly? endDate,
        decimal exemptionRate,
        DateOnly? boardDecisionDate,
        RolePaymentType paymentType = RolePaymentType.Oran,
        decimal? fixedAmount = null,
        RolePeriodType? periodType = null)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.RoleAssignment.ApartmentNotFound);
        }

        if (!Enum.IsDefined(roleType))
        {
            throw new DomainException(DomainConstants.RoleAssignment.NotFound);
        }

        if (!Enum.IsDefined(paymentType))
        {
            throw new DomainException(DomainConstants.RoleAssignment.PaymentTypeInvalid);
        }

        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new DomainException(DomainConstants.RoleAssignment.PersonNameRequired);
        }

        var name = personName.Trim();
        if (name.Length > DomainConstants.RoleAssignment.PersonNameMaxLength)
        {
            throw new DomainException(DomainConstants.RoleAssignment.PersonNameTooLong);
        }

        decimal rate = 0m;
        decimal? resolvedFixedAmount = null;
        RolePeriodType? resolvedPeriodType = null;

        if (paymentType == RolePaymentType.SabitTutar)
        {
            if (fixedAmount is not > 0m || fixedAmount > DomainConstants.BankAccount.AmountMax)
            {
                throw new DomainException(DomainConstants.RoleAssignment.FixedAmountInvalid);
            }

            if (periodType is null || !Enum.IsDefined(periodType.Value))
            {
                throw new DomainException(DomainConstants.RoleAssignment.PeriodTypeRequired);
            }

            resolvedFixedAmount = decimal.Round(fixedAmount.Value, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
            resolvedPeriodType = periodType.Value;
        }
        else
        {
            rate = decimal.Round(exemptionRate, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
            if (rate < 0m || rate > 1m)
            {
                throw new DomainException(DomainConstants.RoleAssignment.RateInvalid);
            }
        }

        if (endDate is { } end && end < startDate)
        {
            throw new DomainException(DomainConstants.RoleAssignment.DateInvalid);
        }

        return new RoleAssignment
        {
            ApartmentId = apartmentId,
            PersonName = name,
            RoleType = roleType,
            StartDate = startDate,
            EndDate = endDate,
            ExemptionRate = rate,
            PaymentType = paymentType,
            FixedAmount = resolvedFixedAmount,
            PeriodType = resolvedPeriodType,
            BoardDecisionDate = boardDecisionDate,
        };
    }

    public bool HasCompensation =>
        !IsCancelled && (PaymentType == RolePaymentType.SabitTutar
            ? FixedAmount is > 0m
            : ExemptionRate > 0m);

    public bool Covers(DateOnly date) =>
        !IsCancelled && StartDate <= date && (EndDate is null || EndDate.Value >= date);

    /// <summary>
    /// Görev tarih aralığının verilen tahakkuk dönemiyle kesişip kesişmediğini döner.
    /// Açık uçlu veya dönem içinde aktif olan görevlerin tahakkuka yansımasını sağlar.
    /// </summary>
    public bool CoversPeriod(DateOnly periodStart, DateOnly periodEnd) =>
        !IsCancelled && StartDate <= periodEnd && (EndDate is null || EndDate.Value >= periodStart);

    public void Close(DateOnly endDate)
    {
        if (IsCancelled)
        {
            throw new DomainException(DomainConstants.RoleAssignment.AlreadyCancelled);
        }

        if (EndDate is not null)
        {
            throw new DomainException(DomainConstants.RoleAssignment.AlreadyClosed);
        }

        if (endDate < StartDate)
        {
            throw new DomainException(DomainConstants.RoleAssignment.DateInvalid);
        }

        EndDate = endDate;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset? cancelledAt = null)
    {
        if (IsCancelled)
        {
            throw new DomainException(DomainConstants.RoleAssignment.AlreadyCancelled);
        }

        IsCancelled = true;
        CancelledAt = cancelledAt ?? DateTimeOffset.UtcNow;
        LastModifiedOn = CancelledAt;
    }

    public static bool RangesOverlap(DateOnly startA, DateOnly? endA, DateOnly startB, DateOnly? endB)
    {
        var untilA = endA ?? DateOnly.MaxValue;
        var untilB = endB ?? DateOnly.MaxValue;
        return startA <= untilB && startB <= untilA;
    }

    /// <summary>
    /// Yeni görevi mevcut satırların yanına yerleştirir. Çakışan açık görev,
    /// yeni başlangıçtan bir gün önce kapanır. Geçmiş çakışması reddedilir.
    /// </summary>
    public static void Place(IEnumerable<RoleAssignment> existing, RoleAssignment candidate)
    {
        var rows = existing.Where(r => !r.IsCancelled).ToList();
        foreach (var row in rows.Where(r => r.EndDate is null))
        {
            if (!RangesOverlap(row.StartDate, null, candidate.StartDate, candidate.EndDate))
            {
                continue;
            }

            if (candidate.StartDate <= row.StartDate)
            {
                throw new DomainException(DomainConstants.RoleAssignment.Overlap);
            }

            row.Close(candidate.StartDate.AddDays(-1));
        }

        foreach (var row in rows)
        {
            if (RangesOverlap(row.StartDate, row.EndDate, candidate.StartDate, candidate.EndDate))
            {
                throw new DomainException(DomainConstants.RoleAssignment.Overlap);
            }
        }
    }
}
