using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Features.Dashboard.Queries.GetBankDashboard;
using Westalife.Application.Features.Dashboard.Queries.GetDashboardSummary;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yönetim panosu özeti: bina, daire/sayaç sayıları, son fatura, kayıp-kaçak % (S3).</summary>
    [HasAnyPermission(
        Permissions.Reports.GeneralAssembly,
        Permissions.Reports.IncomeExpense,
        Permissions.Reports.ApartmentTracking,
        Permissions.Reports.FuelBreakdown,
        Permissions.Reports.BankControl,
        Permissions.Reports.GasNotifications)]
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDashboardSummaryQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Banka durumu: hesap bazında devir, giren, çıkan ve yılın aylık hareketi.</summary>
    [HasPermission(Permissions.Bank.View)]
    [HttpGet("banks")]
    [ProducesResponseType(typeof(BankDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBankDashboardQuery(), cancellationToken);
        return Ok(result);
    }
}
