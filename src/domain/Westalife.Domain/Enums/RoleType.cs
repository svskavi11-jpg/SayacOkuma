namespace Westalife.Domain.Enums;

/// <summary>Site görevi. Muafiyet oranı veya sabit ücret görev kaydında tutulur.</summary>
public enum RoleType
{
    Yonetici = 1,
    Denetci = 2
}

/// <summary>Görev ücreti / muafiyet türü.</summary>
public enum RolePaymentType
{
    /// <summary>Aidat üzerinden yüzdelik muafiyet (0.00 – 1.00).</summary>
    Oran = 1,

    /// <summary>Sabit TL tutarlı huzur hakkı / yönetim ücreti.</summary>
    SabitTutar = 2
}

/// <summary>Sabit tutarlı görev ücretinin periyodu.</summary>
public enum RolePeriodType
{
    Aylik = 1,
    Yillik = 2
}
