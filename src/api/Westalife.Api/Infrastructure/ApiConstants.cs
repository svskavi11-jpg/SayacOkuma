namespace Westalife.Api.Infrastructure;

/// <summary>API katmanı sabitleri (magic string yasağı).</summary>
public static class ApiConstants
{
    public static class Titles
    {
        public const string ValidationFailed = "Doğrulama hatası";
        public const string DomainRuleViolation = "İş kuralı ihlali";
        public const string DataConflict = "Veri çakışması";
        public const string MeteringFailure = "Sayaç okuma hatası";
        public const string ResourceNotFound = "Kayıt bulunamadı";
        public const string UnexpectedError = "Beklenmeyen bir hata oluştu";
    }

    public static class Routes
    {
        public const string ApiPrefix = "api/v1";
    }
}
