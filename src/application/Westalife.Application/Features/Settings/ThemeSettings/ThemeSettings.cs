using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.ThemeSettings;

/// <summary>Site görünümü. Ayrı uç noktadır; oran ve M-Bus güncellemesine karışmaz.</summary>
public sealed record ThemeSettingsResponse(string Theme);

public sealed record GetThemeSettingsQuery : IRequest<ThemeSettingsResponse>;

public sealed class GetThemeSettingsQueryHandler : IRequestHandler<GetThemeSettingsQuery, ThemeSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetThemeSettingsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ThemeSettingsResponse> Handle(GetThemeSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        return new ThemeSettingsResponse(settings.Theme);
    }
}

public sealed record UpdateThemeSettingsCommand(string Theme) : IRequest<ThemeSettingsResponse>;

public sealed class UpdateThemeSettingsCommandHandler : IRequestHandler<UpdateThemeSettingsCommand, ThemeSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateThemeSettingsCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ThemeSettingsResponse> Handle(UpdateThemeSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        settings.UpdateTheme(request.Theme?.Trim() ?? string.Empty);
        await _db.SaveChangesAsync(cancellationToken);
        return new ThemeSettingsResponse(settings.Theme);
    }
}
