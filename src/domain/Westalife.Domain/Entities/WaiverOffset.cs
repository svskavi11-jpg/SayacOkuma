using System.Globalization;
using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Aidat tahakkukunun görev muafiyeti veya sabit huzur hakkı mahsubu.
/// Tutar, akıllı mahsup sıralamasıyla önce Aidat borcundan, yetmezse Isınma borcundan düşer;
/// artan kısım dairenin cari hesabına "Alacak" (negatif bakiye) olarak yansır ve Yönetim gideri gerçekleşenine tam tutar yazılır.
/// </summary>
public class WaiverOffset : BaseEntity
{
    public Guid AidatChargeId { get; private set; }

    public AidatCharge AidatCharge { get; private set; } = null!;

    public Guid RoleAssignmentId { get; private set; }

    public RoleAssignment RoleAssignment { get; private set; } = null!;

    public Guid ApartmentId { get; private set; }

    public Guid BudgetItemId { get; private set; }

    public BudgetItem BudgetItem { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public DateOnly OffsetDate { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Görev mahsubu iptal edildi mi? (İlgili görev iptal edildiğinde true yapılır; Cari Hesap'ta 0 TL denetim izi kalır).</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>Mahsubun iptal edildiği zaman (UTC).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    private WaiverOffset() { }

    public static decimal AmountFor(decimal chargeAmount, decimal rate)
    {
        if (chargeAmount <= 0m || rate <= 0m)
        {
            return 0m;
        }

        var amount = decimal.Round(chargeAmount * rate, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
        return amount > chargeAmount ? chargeAmount : amount;
    }

    /// <summary>
    /// Görev kaydının ücret türüne (Oran % veya Sabit Tutar TL) göre mahsup tutarını hesaplar.
    /// SabitTutar seçildiğinde dairenin aidat tutarıyla sınırlandırılmaz; tam ücret döner.
    /// </summary>
    public static decimal AmountFor(decimal chargeAmount, RoleAssignment role)
    {
        if (role.IsCancelled)
        {
            return 0m;
        }

        if (role.PaymentType == RolePaymentType.SabitTutar)
        {
            if (role.FixedAmount is not > 0m)
            {
                return 0m;
            }

            return decimal.Round(role.FixedAmount.Value, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
        }

        return AmountFor(chargeAmount, role.ExemptionRate);
    }

    public static string Describe(RoleType roleType, string personName, decimal rate)
    {
        var role = roleType == RoleType.Denetci ? "Denetçi" : "Yönetici";
        var percent = (rate * 100m).ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"));
        return $"Görev Mahsubu — {role} {personName.Trim()} (%{percent})";
    }

    public static string Describe(RoleAssignment role)
    {
        if (role.PaymentType == RolePaymentType.SabitTutar && role.FixedAmount is > 0m)
        {
            var roleTitle = role.RoleType == RoleType.Denetci ? "Denetçi" : "Yönetici";
            var periodLabel = role.PeriodType == RolePeriodType.Yillik ? "Yıllık Huzur Hakkı" : "Aylık Huzur Hakkı";
            var formatted = role.FixedAmount.Value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
            return $"Görev Mahsubu — {roleTitle} {role.PersonName.Trim()} ({periodLabel}: {formatted} TL)";
        }

        return Describe(role.RoleType, role.PersonName, role.ExemptionRate);
    }

    public static WaiverOffset Create(
        Guid aidatChargeId,
        Guid roleAssignmentId,
        Guid apartmentId,
        Guid budgetItemId,
        decimal amount,
        DateOnly offsetDate,
        string description)
    {
        if (aidatChargeId == Guid.Empty || roleAssignmentId == Guid.Empty || apartmentId == Guid.Empty || budgetItemId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.RoleAssignment.NotFound);
        }

        var rounded = decimal.Round(amount, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
        if (rounded <= 0m || rounded > DomainConstants.BankAccount.AmountMax)
        {
            throw new DomainException(DomainConstants.BankTransaction.AmountInvalid);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException(DomainConstants.RoleAssignment.PersonNameRequired);
        }

        var text = description.Trim();
        if (text.Length > DomainConstants.WaiverOffset.DescriptionMaxLength)
        {
            throw new DomainException(DomainConstants.BankTransaction.DescriptionTooLong);
        }

        return new WaiverOffset
        {
            AidatChargeId = aidatChargeId,
            RoleAssignmentId = roleAssignmentId,
            ApartmentId = apartmentId,
            BudgetItemId = budgetItemId,
            Amount = rounded,
            OffsetDate = offsetDate,
            Description = text,
        };
    }

    public void Cancel(DateTimeOffset? cancelledAt = null)
    {
        if (IsCancelled)
        {
            return;
        }

        IsCancelled = true;
        CancelledAt = cancelledAt ?? DateTimeOffset.UtcNow;
        LastModifiedOn = CancelledAt;
    }

    /// <summary>
    /// Ayarlar sayfasından Görev Mahsup Bütçe Kalemi değiştirildiğinde mevcut aktif mahsubu yeni kaleme taşır.
    /// </summary>
    public void RebindBudgetItem(Guid newBudgetItemId)
    {
        if (newBudgetItemId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.WaiverOffset.ManagementExpenseMissing);
        }

        if (BudgetItemId == newBudgetItemId)
        {
            return;
        }

        BudgetItemId = newBudgetItemId;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
