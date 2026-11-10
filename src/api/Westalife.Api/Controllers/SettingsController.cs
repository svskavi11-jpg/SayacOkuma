using Westalife.Application.Features.Notifications;
using Westalife.Application.Features.Settings.BudgetSettings;
using Westalife.Application.Features.Settings.InterestSettings;
using Westalife.Application.Features.Settings.ManagementPeriodSettings;
using Westalife.Application.Features.Settings.RoleFeeSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Settings.GetAppSettings;
using Westalife.Application.Features.Settings.NoticeSettings;
using Westalife.Application.Features.Settings.TestMbusConnection;
using Westalife.Application.Features.Settings.ThemeSettings;
using Westalife.Application.Features.Settings.UpdateAppSettings;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/settings")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Sistem ayarlarını döndürür (şirket adı, bina adı, 30/70 oranları, M-Bus donanım ayarları).</summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Settings.MBus)]
    [HttpGet]
    [ProducesResponseType(typeof(AppSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAppSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sistem ayarlarını günceller. Sabit + Tüketim oranı tam %100 olmalıdır;
    /// aynı transaction'da Building tablosuna aynalanır (bina adı + sabit gider oranı).
    /// M-Bus donanım ayarları da buradan güncellenir (bağlantı tipi, TCP/USB parametreleri).
    /// </summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Settings.MBus)]
    [HttpPut]
    [ProducesResponseType(typeof(AppSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateAppSettingsCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// "Bağlantıyı Test Et": DB'deki M-Bus ayarlarıyla Izar Center'a (TCP) / COM portuna (USB)
    /// ulaşılmaya çalışılır; Simulator her zaman başarılı döner.
    /// </summary>
    [HasPermission(Permissions.Settings.MBus)]
    [HttpPost("test-mbus-connection")]
    [ProducesResponseType(typeof(MbusConnectionTestResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestMbusConnection(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TestMbusConnectionCommand(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Bireysel Gider Bildirimi form ayarları (başlık metinleri, son ödeme günü, hizmet bedeli, açıklama, adres).</summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Reports.GasNotifications)]
    [HttpGet("notice")]
    [ProducesResponseType(typeof(NoticeSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNoticeSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetNoticeSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("notice")]
    [ProducesResponseType(typeof(NoticeSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateNoticeSettings(
        [FromBody] UpdateNoticeSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Sol/sağ logoyu indirir (side: left | right). Yüklenmemişse 404.</summary>
    [HasPermission(Permissions.Settings.General)]
    [HttpGet("notice/logo/{side}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNoticeLogo(string side, CancellationToken cancellationToken)
    {
        var logo = await _mediator.Send(new GetNoticeLogoQuery(side), cancellationToken);
        return logo is null ? NotFound() : File(logo.Content, logo.ContentType);
    }

    /// <summary>Logo yükler (PNG/JPG, en fazla 1 MB). side: left | right.</summary>
    [HasPermission(Permissions.Settings.General)]
    [HttpPut("notice/logo/{side}")]
    [RequestSizeLimit(2_000_000)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadNoticeLogo(
        string side, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        await _mediator.Send(new SetNoticeLogoCommand(side, stream.ToArray(), file.ContentType), cancellationToken);
        return NoContent();
    }

    [HasPermission(Permissions.Settings.General)]
    [HttpDelete("notice/logo/{side}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveNoticeLogo(string side, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveNoticeLogoCommand(side), cancellationToken);
        return NoContent();
    }

    /// <summary>Faz 6.8: gecikme faizi ayarları (açık/kapalı, günlük periyot, yıllık basit faiz oranı).</summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Payments.View)]
    [HttpGet("interest")]
    [ProducesResponseType(typeof(InterestSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInterestSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetInterestSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("interest")]
    [ProducesResponseType(typeof(InterestSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateInterestSettings(
        [FromBody] UpdateInterestSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Faz 6.10: bütçe takibi anahtarı. Kapalıyken gider serbest tür ile girilir.</summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Budget.View, Permissions.Reports.GasNotifications, Permissions.Reports.IncomeExpense)]
    [HttpGet("budget")]
    [ProducesResponseType(typeof(BudgetSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBudgetSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetBudgetSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("budget")]
    [ProducesResponseType(typeof(BudgetSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBudgetSettings(
        [FromBody] UpdateBudgetSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Aktif yönetim dönemi (başlangıç ve bitiş tarihi) ayarları.</summary>
    [HasAnyPermission(Permissions.Settings.General, Permissions.Budget.View, Permissions.Reports.GeneralAssembly, Permissions.Reports.IncomeExpense, Permissions.Reports.ApartmentTracking, Permissions.Reports.FuelBreakdown, Permissions.Reports.BankControl, Permissions.Reports.GasNotifications)]
    [HttpGet("management-period")]
    [ProducesResponseType(typeof(ManagementPeriodSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagementPeriodSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetManagementPeriodSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("management-period")]
    [ProducesResponseType(typeof(ManagementPeriodSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateManagementPeriodSettings(
        [FromBody] UpdateManagementPeriodSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Varsayılan görev ücretleri (Yönetici ve Denetçi için Oran veya Sabit Tutar / Huzur Hakkı).</summary>
    [HasPermission(Permissions.Settings.General)]
    [HttpGet("role-fees")]
    [ProducesResponseType(typeof(RoleFeeSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoleFeeSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetRoleFeeSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("role-fees")]
    [ProducesResponseType(typeof(RoleFeeSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRoleFeeSettings(
        [FromBody] UpdateRoleFeeSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Site teması. Oran ve M-Bus kaydından ayrıdır; yalnızca Theme kolonunu değiştirir.</summary>
    [Authorize]
    [HttpGet("theme")]
    [ProducesResponseType(typeof(ThemeSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTheme(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetThemeSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.General)]
    [HttpPut("theme")]
    [ProducesResponseType(typeof(ThemeSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTheme(
        [FromBody] UpdateThemeSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>SMTP, SMS ve WhatsApp kimlik bilgileri. Şifre ve token yanıtta dönmez.</summary>
    [HasPermission(Permissions.Settings.Notification)]
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(NotificationSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetNotificationSettingsQuery(), cancellationToken));

    [HasPermission(Permissions.Settings.Notification)]
    [HttpPut("notifications")]
    [ProducesResponseType(typeof(NotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateNotificationSettings(
        [FromBody] UpdateNotificationSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));
}
