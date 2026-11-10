namespace Westalife.Domain.Exceptions;

/// <summary>Kimliği doğrulanmış kullanıcının bu kaynağa yetkisi yok.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
