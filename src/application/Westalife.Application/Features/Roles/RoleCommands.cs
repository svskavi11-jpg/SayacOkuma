using FluentValidation;
using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Roles;

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleListItem>>;

public sealed class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleListItem>>
{
    private readonly IRoleAdmin _roles;
    private readonly ITenantContext _tenant;

    public ListRolesQueryHandler(IRoleAdmin roles, ITenantContext tenant)
    {
        _roles = roles;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<RoleListItem>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Önce site seçin.");
        }

        return _roles.ListAsync(_tenant.TenantId, cancellationToken);
    }
}

public sealed record PermissionCatalogItem(string Code, string Group, string Label);

public sealed record ListPermissionCatalogQuery : IRequest<IReadOnlyList<PermissionCatalogItem>>;

public sealed class ListPermissionCatalogQueryHandler : IRequestHandler<ListPermissionCatalogQuery, IReadOnlyList<PermissionCatalogItem>>
{
    public Task<IReadOnlyList<PermissionCatalogItem>> Handle(ListPermissionCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PermissionCatalogItem>>(
            Permissions.Catalog.Select(item => new PermissionCatalogItem(item.Code, item.Group, item.Label)).ToArray());
}

public sealed record SaveRoleBody(string Name, IReadOnlyList<string> Permissions);

public sealed record CreateRoleCommand(string Name, IReadOnlyList<string> Permissions) : IRequest<Guid>;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Permissions).NotEmpty();
    }
}

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly IRoleAdmin _roles;
    private readonly ITenantContext _tenant;

    public CreateRoleCommandHandler(IRoleAdmin roles, ITenantContext tenant)
    {
        _roles = roles;
        _tenant = tenant;
    }

    public Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Önce site seçin.");
        }

        return _roles.CreateAsync(_tenant.TenantId, request.Name, request.Permissions, cancellationToken);
    }
}

public sealed record UpdateRoleCommand(Guid Id, string Name, IReadOnlyList<string> Permissions) : IRequest;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Permissions).NotEmpty();
    }
}

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand>
{
    private readonly IRoleAdmin _roles;
    private readonly ITenantContext _tenant;

    public UpdateRoleCommandHandler(IRoleAdmin roles, ITenantContext tenant)
    {
        _roles = roles;
        _tenant = tenant;
    }

    public Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Önce site seçin.");
        }

        return _roles.UpdateAsync(_tenant.TenantId, request.Id, request.Name, request.Permissions, cancellationToken);
    }
}

public sealed record DeleteRoleCommand(Guid Id) : IRequest;

public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly IRoleAdmin _roles;
    private readonly ITenantContext _tenant;

    public DeleteRoleCommandHandler(IRoleAdmin roles, ITenantContext tenant)
    {
        _roles = roles;
        _tenant = tenant;
    }

    public Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Önce site seçin.");
        }

        return _roles.DeleteAsync(_tenant.TenantId, request.Id, cancellationToken);
    }
}

public sealed record ListAssignableRolesQuery(Guid TenantId) : IRequest<IReadOnlyList<AssignableRoleItem>>;

public sealed record AssignableRoleItem(Guid Id, string Name);

public sealed class ListAssignableRolesQueryHandler : IRequestHandler<ListAssignableRolesQuery, IReadOnlyList<AssignableRoleItem>>
{
    private readonly IRoleAdmin _roles;

    public ListAssignableRolesQueryHandler(IRoleAdmin roles) => _roles = roles;

    public async Task<IReadOnlyList<AssignableRoleItem>> Handle(ListAssignableRolesQuery request, CancellationToken cancellationToken)
    {
        var items = await _roles.ListAsync(request.TenantId, cancellationToken);
        return items.Select(item => new AssignableRoleItem(item.Id, item.Name)).ToArray();
    }
}
