using Westalife.Application.Common.Enums;

namespace Westalife.Application.Common.Models;

/// <summary>Tek bir sayaç okuma denemesinin sonucu. Başarısızlıkta Snapshot null, ErrorCode doludur.</summary>
public sealed record MeterReadResult
{
    public string? SerialNumber { get; init; }

    public byte PrimaryAddress { get; init; }

    public bool IsSuccess { get; init; }

    public MeterSnapshot? Snapshot { get; init; }

    public MeterReadErrorCode ErrorCode { get; init; } = MeterReadErrorCode.None;

    public string? ErrorMessage { get; init; }

    public static MeterReadResult Success(MeterSnapshot snapshot) => new()
    {
        IsSuccess = true,
        SerialNumber = snapshot.SerialNumber,
        PrimaryAddress = snapshot.PrimaryAddress,
        Snapshot = snapshot
    };

    public static MeterReadResult Failure(string? serialNumber, byte primaryAddress, MeterReadErrorCode errorCode, string errorMessage) =>
        new()
        {
            IsSuccess = false,
            SerialNumber = serialNumber,
            PrimaryAddress = primaryAddress,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}
