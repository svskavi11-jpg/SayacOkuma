namespace Westalife.Domain.Common;

/// <summary>
/// Denetim damgası. Oturumdaki ad ve kullanıcı kimliği birlikte tutulur;
/// oturum yoksa (tohum, sayaç döngüsü) <see cref="System"/> yazılır.
/// </summary>
public static class AuditActor
{
    public const int MaxLength = 200;

    public const string System = "Sistem";

    public static string Format(string? fullName, string? userId)
    {
        var name = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        var id = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

        var text = name is null && id is null
            ? System
            : name is null
                ? id!
                : id is null
                    ? name
                    : $"{name} ({id})";

        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
