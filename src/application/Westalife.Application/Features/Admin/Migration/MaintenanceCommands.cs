using MediatR;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Admin.Migration;

public sealed record MaintenanceStatus(bool Maintenance, DateTimeOffset? Since);

public sealed record GetMaintenanceStatusQuery : IRequest<MaintenanceStatus>;

public sealed class GetMaintenanceStatusQueryHandler : IRequestHandler<GetMaintenanceStatusQuery, MaintenanceStatus>
{
    private readonly IMaintenanceMode _maintenance;

    public GetMaintenanceStatusQueryHandler(IMaintenanceMode maintenance) => _maintenance = maintenance;

    public Task<MaintenanceStatus> Handle(GetMaintenanceStatusQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Read(_maintenance));

    internal static MaintenanceStatus Read(IMaintenanceMode maintenance) =>
        new(maintenance.IsEnabled, maintenance.EnabledOn);
}

public sealed record SetMaintenanceModeCommand(bool Enabled) : IRequest<MaintenanceStatus>;

public sealed class SetMaintenanceModeCommandHandler : IRequestHandler<SetMaintenanceModeCommand, MaintenanceStatus>
{
    private readonly IMaintenanceMode _maintenance;

    public SetMaintenanceModeCommandHandler(IMaintenanceMode maintenance) => _maintenance = maintenance;

    public Task<MaintenanceStatus> Handle(SetMaintenanceModeCommand request, CancellationToken cancellationToken)
    {
        if (request.Enabled)
        {
            _maintenance.Enable();
        }
        else
        {
            _maintenance.Disable();
        }

        return Task.FromResult(GetMaintenanceStatusQueryHandler.Read(_maintenance));
    }
}
