namespace Westalife.Domain.Enums;

/// <summary>Sayaç tipi. Bina ana doğalgaz sayacı (m³) ile daire kalorimetreleri (kWh) ayrı tutulur.</summary>
public enum MeterType
{
    /// <summary>Danfoss kalorimetre — daire bazlı, kWh ölçer (M-Bus üzerinden okunur).</summary>
    Calorimeter = 1,

    /// <summary>Bina ana doğalgaz sayacı — m³ ölçer; kWh'e faturadaki K katsayısı ve üst ısıl değer ile çevrilir.</summary>
    GasMasterMeter = 2
}
