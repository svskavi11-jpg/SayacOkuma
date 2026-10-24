using MediatR;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;
using Westalife.Domain.Enums;

namespace Westalife.Application.Features.MeterReadings.Commands.RecordMeterReadings;

/// <summary>
/// Okuma motorundan gelen snapshot'ları MeterReading tablosuna kaydeder (idempotent).
/// Duplicate (aynı sayaç + aynı zaman) ve cihaz hatası bildiren okumalar atlanır, sayaçları
/// sistemde kayıtlı olmayan snapshot'lar "eşleşmedi" olarak raporlanır.
/// </summary>
public sealed record RecordMeterReadingsCommand(
    IReadOnlyList<MeterSnapshot> Readings,
    ReadingSource Source) : IRequest<RecordMeterReadingsResult>;

public sealed record RecordMeterReadingsResult(
    int InsertedCount,
    int SkippedDuplicateCount,
    int SkippedDeviceErrorCount,
    int UnmatchedSerialCount);
