namespace Westalife.Domain.Exceptions;

/// <summary>
/// Talep edilen kaynak bulunamadığında fırlatılan istisna (404).
/// GlobalExceptionHandler bu tipi <c>InvalidOperationException</c>'dan ayırt ederek
/// doğru HTTP 404 döner — diğer InvalidOperationException'lar 500 olarak işlenir.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} bulunamadı. (Id: {key})")
    {
    }
}
