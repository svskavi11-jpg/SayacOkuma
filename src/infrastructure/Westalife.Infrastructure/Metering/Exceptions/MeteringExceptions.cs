namespace Westalife.Infrastructure.Metering.Exceptions;

/// <summary>Okuma motoru hatalarının temel sınıfı.</summary>
public class MeteringException : Exception
{
    public MeteringException(string message) : base(message)
    {
    }

    public MeteringException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Transport bağlantısı açılamadı / koptu.</summary>
public class MbusConnectionException : MeteringException
{
    public MbusConnectionException(string message) : base(message)
    {
    }

    public MbusConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Cihaz zamanında yanıt vermedi.</summary>
public class MbusTimeoutException : MeteringException
{
    public MbusTimeoutException(string message) : base(message)
    {
    }

    public MbusTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Yanıt frame'i EN 13757 kurallarına uymuyor.</summary>
public class MbusProtocolException : MeteringException
{
    /// <summary>Hatalı gelen telegramın hex temsili (RawTelegramHex loglama stratejisinin parçası).</summary>
    public string? RawTelegramHex { get; }

    public MbusProtocolException(string message, string? rawTelegramHex = null) : base(message)
    {
        RawTelegramHex = rawTelegramHex;
    }
}

/// <summary>Yapılandırma hatalı (bağlantı tipi, port adı, host vb.).</summary>
public class MbusConfigurationException : MeteringException
{
    public MbusConfigurationException(string message) : base(message)
    {
    }

    public MbusConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
