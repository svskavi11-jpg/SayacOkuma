using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.BudgetSettings;

/// <summary>Faz 6.10: Bütçe takibi anahtarı. Kapalıyken gider serbest tür ile girilir.</summary>
public sealed record BudgetSettingsResponse(bool IsBudgetTrackingEnabled);

public sealed record GetBudgetSettingsQuery : IRequest<BudgetSettingsResponse>;

public sealed class GetBudgetSettingsQueryHandler : IRequestHandler<GetBudgetSettingsQuery, BudgetSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetBudgetSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BudgetSettingsResponse> Handle(GetBudgetSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        return new BudgetSettingsResponse(settings.IsBudgetTrackingEnabled);
    }
}

public sealed record UpdateBudgetSettingsCommand(bool IsBudgetTrackingEnabled) : IRequest<BudgetSettingsResponse>;

public sealed class UpdateBudgetSettingsCommandHandler
    : IRequestHandler<UpdateBudgetSettingsCommand, BudgetSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateBudgetSettingsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BudgetSettingsResponse> Handle(UpdateBudgetSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        settings.UpdateBudgetTracking(request.IsBudgetTrackingEnabled);
        await _db.SaveChangesAsync(cancellationToken);

        return new BudgetSettingsResponse(settings.IsBudgetTrackingEnabled);
    }
}
