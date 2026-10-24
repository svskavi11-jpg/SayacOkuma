using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.NoticeSettings;

/// <summary>
/// Bireysel Gider Bildirimi (PDF) form ayarları: başlık metinleri, son ödeme gün sayısı,
/// okuma hizmet bedeli, varsayılan açıklama, bina adresi. Logolar ayrı komutlarla yönetilir.
/// </summary>
public sealed record NoticeSettingsResponse(
    string? LeftTitle,
    string? RightTitle,
    int DueDateDays,
    decimal ReadingServiceFee,
    string? DefaultNote,
    string? DefaultSubscriberNumber,
    string BuildingAddress,
    bool HasLeftLogo,
    bool HasRightLogo,
    int LogoLeftHeightMm,
    int LogoRightHeightMm);

public sealed record GetNoticeSettingsQuery : IRequest<NoticeSettingsResponse>;

public sealed class GetNoticeSettingsQueryHandler : IRequestHandler<GetNoticeSettingsQuery, NoticeSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetNoticeSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NoticeSettingsResponse> Handle(GetNoticeSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        var address = await _db.Buildings.AsNoTracking()
            .Where(b => b.IsActive).Select(b => b.Address).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new NoticeSettingsResponse(
            settings.NoticeLeftTitle,
            settings.NoticeRightTitle,
            settings.DueDateDays,
            settings.ReadingServiceFee,
            settings.DefaultNoticeNote,
            settings.DefaultSubscriberNumber,
            address,
            settings.LogoLeft is { Length: > 0 },
            settings.LogoRight is { Length: > 0 },
            settings.LogoLeftHeightMm,
            settings.LogoRightHeightMm);
    }
}

public sealed record UpdateNoticeSettingsCommand(
    string? LeftTitle,
    string? RightTitle,
    int DueDateDays,
    decimal ReadingServiceFee,
    string? DefaultNote,
    string? DefaultSubscriberNumber,
    string BuildingAddress,
    int LogoLeftHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault,
    int LogoRightHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault) : IRequest<NoticeSettingsResponse>;

public sealed class UpdateNoticeSettingsCommandValidator : AbstractValidator<UpdateNoticeSettingsCommand>
{
    public UpdateNoticeSettingsCommandValidator()
    {
        RuleFor(c => c.LeftTitle).MaximumLength(DomainConstants.AppSettings.NoticeTitleMaxLength);
        RuleFor(c => c.RightTitle).MaximumLength(DomainConstants.AppSettings.NoticeTitleMaxLength);
        RuleFor(c => c.DueDateDays).InclusiveBetween(0, DomainConstants.AppSettings.DueDateDaysMax)
            .WithMessage(DomainConstants.AppSettings.DueDateDaysInvalid);
        RuleFor(c => c.ReadingServiceFee).GreaterThanOrEqualTo(0m)
            .WithMessage(DomainConstants.AppSettings.ReadingServiceFeeInvalid);
        RuleFor(c => c.DefaultNote).MaximumLength(DomainConstants.AppSettings.NoticeNoteMaxLength);
        RuleFor(c => c.DefaultSubscriberNumber).MaximumLength(DomainConstants.AppSettings.SubscriberNumberMaxLength);
        RuleFor(c => c.BuildingAddress).NotEmpty().MaximumLength(DomainConstants.Building.AddressMaxLength)
            .WithMessage(DomainConstants.ValidationMessages.BuildingAddressRequired);
        RuleFor(c => c.LogoLeftHeightMm).InclusiveBetween(
            DomainConstants.AppSettings.LogoHeightMmMin, DomainConstants.AppSettings.LogoHeightMmMax)
            .WithMessage(DomainConstants.AppSettings.LogoHeightInvalid);
        RuleFor(c => c.LogoRightHeightMm).InclusiveBetween(
            DomainConstants.AppSettings.LogoHeightMmMin, DomainConstants.AppSettings.LogoHeightMmMax)
            .WithMessage(DomainConstants.AppSettings.LogoHeightInvalid);
    }
}

public sealed class UpdateNoticeSettingsCommandHandler
    : IRequestHandler<UpdateNoticeSettingsCommand, NoticeSettingsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UpdateNoticeSettingsCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<NoticeSettingsResponse> Handle(UpdateNoticeSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.IsActive, cancellationToken);

        settings.UpdateNoticeSettings(
            request.LeftTitle, request.RightTitle, request.DueDateDays, request.ReadingServiceFee,
            request.DefaultNote, request.DefaultSubscriberNumber);
        settings.SetLogoSizes(request.LogoLeftHeightMm, request.LogoRightHeightMm);

        // Adres Building tablosunda tutulur (PDF sol blok oradan okunur).
        building?.UpdateDetails(building.Name, request.BuildingAddress.Trim());

        await _db.SaveChangesAsync(cancellationToken);
        return await _mediator.Send(new GetNoticeSettingsQuery(), cancellationToken);
    }
}

/// <summary>Logo yükleme/kaldırma ve indirme.</summary>
public sealed record SetNoticeLogoCommand(string Side, byte[] Content, string ContentType) : IRequest;

public sealed class SetNoticeLogoCommandHandler : IRequestHandler<SetNoticeLogoCommand>
{
    private readonly IApplicationDbContext _db;

    public SetNoticeLogoCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetNoticeLogoCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        settings.SetLogo(request.Side, request.Content, request.ContentType);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record RemoveNoticeLogoCommand(string Side) : IRequest;

public sealed class RemoveNoticeLogoCommandHandler : IRequestHandler<RemoveNoticeLogoCommand>
{
    private readonly IApplicationDbContext _db;

    public RemoveNoticeLogoCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveNoticeLogoCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");
        settings.RemoveLogo(request.Side);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record NoticeLogoFile(byte[] Content, string ContentType);

public sealed record GetNoticeLogoQuery(string Side) : IRequest<NoticeLogoFile?>;

public sealed class GetNoticeLogoQueryHandler : IRequestHandler<GetNoticeLogoQuery, NoticeLogoFile?>
{
    private readonly IApplicationDbContext _db;

    public GetNoticeLogoQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NoticeLogoFile?> Handle(GetNoticeLogoQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        return request.Side switch
        {
            DomainConstants.AppSettings.LogoSideLeft when settings.LogoLeft is { Length: > 0 } =>
                new NoticeLogoFile(settings.LogoLeft, settings.LogoLeftContentType ?? "image/png"),
            DomainConstants.AppSettings.LogoSideRight when settings.LogoRight is { Length: > 0 } =>
                new NoticeLogoFile(settings.LogoRight, settings.LogoRightContentType ?? "image/png"),
            DomainConstants.AppSettings.LogoSideLeft or DomainConstants.AppSettings.LogoSideRight => null,
            _ => throw new DomainException(DomainConstants.AppSettings.LogoSideInvalid),
        };
    }
}
