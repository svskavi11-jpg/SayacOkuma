namespace Westalife.Infrastructure.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "westalife";

    public string Audience { get; set; } = "westalife";

    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;

    public bool SeedUsers { get; set; }

    public string SeedPassword { get; set; } = string.Empty;
}
