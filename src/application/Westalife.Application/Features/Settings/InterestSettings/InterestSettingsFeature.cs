using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.InterestSettings;

/// <summary>
/// Gecikme faizi ayarları (Faz 6.8): açık/kapalı, periyot (yalnız günlük), YILLIK basit faiz oranı (%).
/// Faiz = Anapara × Oran/100 × Gün/365; bileşik faiz yoktur.
/// </summary>
public sealed record InterestSettingsResponse(bool IsInterestEnabled, string InterestPeriod, decimal InterestRate);

public sealed record GetInterestSettingsQuery : IRequest<InterestSettingsResponse>;

public sealed class GetInterestSettingsQueryHandler : IRequestHandler<GetInterestSettingsQuery, InterestSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetInterestSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InterestSettingsResponse> Handle(GetInterestSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        return new InterestSettingsResponse(settings.IsInterestEnabled, settings.InterestPeriod, settings.InterestRate);
    }
}

public sealed record UpdateInterestSettingsCommand(
    bool IsInterestEnabled,
    string InterestPeriod,
    decimal InterestRate) : IRequest<InterestSettingsResponse>;

public sealed class UpdateInterestSettingsCommandValidator : AbstractValidator<UpdateInterestSettingsCommand>
{
    public UpdateInterestSettingsCommandValidator()
    {
        RuleFor(c => c.InterestPeriod).Equal(DomainConstants.AppSettings.InterestPeriodDaily)
            .WithMessage(DomainConstants.AppSettings.InterestPeriodInvalid);
        RuleFor(c => c.InterestRate).InclusiveBetween(0m, DomainConstants.AppSettings.InterestRateMax)
            .WithMessage(DomainConstants.AppSettings.InterestRateInvalid);
    }
}

public sealed class UpdateInterestSettingsCommandHandler
    : IRequestHandler<UpdateInterestSettingsCommand, InterestSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateInterestSettingsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InterestSettingsResponse> Handle(UpdateInterestSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        settings.UpdateInterestSettings(request.IsInterestEnabled, request.InterestPeriod, request.InterestRate);
        await _db.SaveChangesAsync(cancellationToken);

        return new InterestSettingsResponse(settings.IsInterestEnabled, settings.InterestPeriod, settings.InterestRate);
    }
}
