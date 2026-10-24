using MediatR;

namespace Westalife.Application.Features.Meters.Commands.ImportMeters;

/// <summary>Izar Center dışa aktarımından gelen tek sayaç satırı.</summary>
public sealed record ImportedMeterItem(int DoorNumber, string SerialNumber, int PrimaryAddress);

/// <summary>
/// Izar Center okuma listesindeki sayaçların toplu kaydı. Deterministik: duplicate seri no'lar atlanır,
/// daire eşleşmeyen satırlar raporlanır; işlem tek transaction'da (tek SaveChanges) yapılır.
/// </summary>
public sealed record ImportMetersFromIzarCenterCommand(IReadOnlyList<ImportedMeterItem> Items)
    : IRequest<ImportMetersResult>;

public sealed record ImportMetersResult(int ImportedCount, int SkippedExistingCount, int UnmatchedApartmentCount);
