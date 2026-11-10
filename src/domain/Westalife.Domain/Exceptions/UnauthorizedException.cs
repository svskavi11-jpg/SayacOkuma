namespace Westalife.Domain.Exceptions;

/// <summary>E-posta veya şifre kabul edilmediğinde fırlatılır.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
