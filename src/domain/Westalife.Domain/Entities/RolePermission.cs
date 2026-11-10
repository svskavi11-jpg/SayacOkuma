namespace Westalife.Domain.Entities;

/// <summary>Bir giriş rolüne verilmiş yetki kodu.</summary>
public class RolePermission
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public AppRole Role { get; set; } = null!;

    public string Permission { get; set; } = string.Empty;
}
