using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Application.Features.Notifications;

/// <summary>
/// Aidat / ısınma / genel / tüm borçlular gönderiminde şablon eşlemesi ve varsayılan metin.
/// </summary>
public static class NotificationPurposeCatalog
{
    private static readonly string[] AidatTokens = ["[AidatBorc]", "[BuDonemAidat]"];
    private static readonly string[] HeatingTokens = ["[IsinmaBorc]", "[BuDonemIsinma]", "[PdfLink]"];
    private static readonly string[] AllDebtorTokens = ["[Tutar]", "[AidatBorc]", "[IsinmaBorc]"];

    public static string DefaultBody(NotificationPurpose purpose) => purpose switch
    {
        NotificationPurpose.Aidat => DomainConstants.Notification.DefaultAidatBody,
        NotificationPurpose.Heating => DomainConstants.Notification.DefaultHeatingWithPdfBody,
        NotificationPurpose.AllDebtors => DomainConstants.Notification.DefaultAllDebtorsBody,
        _ => DomainConstants.Notification.DefaultBody,
    };

    public static bool MatchesTemplate(NotificationPurpose purpose, string? name, string? body)
    {
        if (purpose == NotificationPurpose.General)
        {
            return true;
        }

        var haystack = $"{name} {body}";
        var tokens = purpose switch
        {
            NotificationPurpose.Aidat => AidatTokens,
            NotificationPurpose.Heating => HeatingTokens,
            NotificationPurpose.AllDebtors => AllDebtorTokens,
            _ => [],
        };

        if (tokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return purpose switch
        {
            NotificationPurpose.Aidat =>
                haystack.Contains("aidat", StringComparison.OrdinalIgnoreCase),
            NotificationPurpose.Heating =>
                haystack.Contains("isinma", StringComparison.OrdinalIgnoreCase)
                || haystack.Contains("ısınma", StringComparison.OrdinalIgnoreCase)
                || haystack.Contains("dogalgaz", StringComparison.OrdinalIgnoreCase)
                || haystack.Contains("doğalgaz", StringComparison.OrdinalIgnoreCase),
            NotificationPurpose.AllDebtors =>
                haystack.Contains("borc", StringComparison.OrdinalIgnoreCase)
                || haystack.Contains("borç", StringComparison.OrdinalIgnoreCase)
                || haystack.Contains("tutar", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }
}
