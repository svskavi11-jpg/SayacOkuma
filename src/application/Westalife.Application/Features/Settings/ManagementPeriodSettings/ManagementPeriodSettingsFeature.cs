using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.ManagementPeriodSettings;

/// <summary>
/// Aktif Yönetim Dönemi (Başlangıç ve Bitiş Tarihi) ayarları.
/// Raporlar ve Genel Kurul / Denetim Raporu varsayılan olarak bu aralığı kullanır.
/// </summary>
public sealed record ManagementPeriodSettingsResponse(DateOnly StartDate, DateOnly EndDate);

public sealed record GetManagementPeriodSettingsQuery : IRequest<ManagementPeriodSettingsResponse>;

public sealed class GetManagementPeriodSettingsQueryHandler
    : IRequestHandler<GetManagementPeriodSettingsQuery, ManagementPeriodSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetManagementPeriodSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ManagementPeriodSettingsResponse> Handle(
        GetManagementPeriodSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        var currentYear = DateTimeOffset.UtcNow.Year;
        var start = settings.ManagementPeriodStart ?? new DateOnly(currentYear, 1, 1);
        var end = settings.ManagementPeriodEnd ?? new DateOnly(currentYear, 12, 31);
        return new ManagementPeriodSettingsResponse(start, end);
    }
}

public sealed record UpdateManagementPeriodSettingsCommand(DateOnly StartDate, DateOnly EndDate)
    : IRequest<ManagementPeriodSettingsResponse>;

public sealed class UpdateManagementPeriodSettingsCommandValidator
    : AbstractValidator<UpdateManagementPeriodSettingsCommand>
{
    public UpdateManagementPeriodSettingsCommandValidator()
    {
        RuleFor(c => c.StartDate).NotEmpty();
        RuleFor(c => c.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(c => c.StartDate)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class UpdateManagementPeriodSettingsCommandHandler
    : IRequestHandler<UpdateManagementPeriodSettingsCommand, ManagementPeriodSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateManagementPeriodSettingsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ManagementPeriodSettingsResponse> Handle(
        UpdateManagementPeriodSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        settings.UpdateManagementPeriod(request.StartDate, request.EndDate);
        await _db.SaveChangesAsync(cancellationToken);

        return new ManagementPeriodSettingsResponse(
            settings.ManagementPeriodStart!.Value,
            settings.ManagementPeriodEnd!.Value);
    }
}

