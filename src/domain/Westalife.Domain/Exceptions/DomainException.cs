namespace Westalife.Domain.Exceptions;

/// <summary>Domain kuralları ihlal edildiğinde fırlatılan temel istisna. Hata yönetimi (Faz 4) bu tipleri yakalar.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
