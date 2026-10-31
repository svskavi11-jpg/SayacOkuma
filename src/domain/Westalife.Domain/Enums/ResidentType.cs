namespace Westalife.Domain.Enums;

/// <summary>
/// Sakin türü (Faz 7.1). Bir dairenin aynı anda 1 aktif Ev Sahibi VE 1 aktif Kiracı olabilir.
/// Borç sahipliği (Hibrit Model — KMK m.22): kiracı varsa borç kiracıya yazılır,
/// ev sahibi LegalResponsibleOwnerId ile müteselsil sorumlu olarak kayıt altına alınır.
/// </summary>
public enum ResidentType
{
    /// <summary>Ev Sahibi (kat maliki).</summary>
    EvSahibi = 1,

    /// <summary>Kiracı (oturan).</summary>
    Kiraci = 2
}
