using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Admin.Tenants;

public sealed record TenantListItem(
    Guid Id,
    string Name,
    string Address,
    bool IsActive,
    int UserCount,
    DateTimeOffset CreatedOn);

public sealed record GetTenantsQuery : IRequest<IReadOnlyList<TenantListItem>>;

public sealed class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantListItem>>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantUserProvisioner _users;

    public GetTenantsQueryHandler(IApplicationDbContext db, ITenantUserProvisioner users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IReadOnlyList<TenantListItem>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var counts = await _users.CountByTenantAsync(cancellationToken);
        var tenants = await _db.Tenants.AsNoTracking()
            .OrderBy(tenant => tenant.Name)
            .ToListAsync(cancellationToken);

        return tenants
            .Select(tenant => new TenantListItem(
                tenant.Id,
                tenant.Name,
                tenant.Address,
                tenant.IsActive,
                counts.GetValueOrDefault(tenant.Id),
                tenant.CreatedOn))
            .ToList();
    }
}

public sealed record CreateTenantCommand(
    string Name,
    string? Address,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword) : IRequest<Guid>;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().WithMessage("Site adı zorunludur.")
            .MaximumLength(DomainConstants.Tenant.NameMaxLength).WithMessage(DomainConstants.ValidationMessages.TenantNameTooLong);
        RuleFor(command => command.Address)
            .MaximumLength(DomainConstants.Tenant.AddressMaxLength).WithMessage(DomainConstants.ValidationMessages.TenantAddressTooLong);
        RuleFor(command => command.AdminFullName).NotEmpty().WithMessage("Yönetici adı zorunludur.")
            .MaximumLength(DomainConstants.Auth.FullNameMaxLength).WithMessage("Yönetici adı en fazla 150 karakter olabilir.");
        RuleFor(command => command.AdminEmail).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta girin.");
        RuleFor(command => command.AdminPassword).ApplyPasswordRules();
    }
}

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantUserProvisioner _users;

    public CreateTenantCommandHandler(IApplicationDbContext db, ITenantUserProvisioner users)
    {
        _db = db;
        _users = users;
    }

    public async Task<Guid> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var address = request.Address?.Trim() ?? string.Empty;
        var duplicate = await _db.Tenants.AnyAsync(tenant => tenant.Name.ToLower() == name.ToLower(), cancellationToken);
        if (duplicate)
        {
            throw new DomainException("Bu site adı zaten kayıtlı.");
        }

        if (await _users.EmailExistsAsync(request.AdminEmail, cancellationToken))
        {
            throw new DomainException("Bu e-posta zaten kayıtlı.");
        }

        var tenant = new Tenant(name, address);
        var buildingAddress = string.IsNullOrWhiteSpace(address) ? name : address;
        var building = new Building(name, buildingAddress);
        building.AssignTenant(tenant.Id);
        var ratio = DomainConstants.Building.DefaultFixedCostRatio;
        var settings = new AppSettings(name, name, ratio, 1m - ratio);
        settings.AssignTenant(tenant.Id);

        _db.Tenants.Add(tenant);
        _db.Buildings.Add(building);
        _db.AppSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);

        await _users.CreateAsync(
            request.AdminEmail,
            request.AdminPassword,
            request.AdminFullName,
            tenant.Id,
            DomainConstants.Auth.TenantAdmin,
            cancellationToken);

        return tenant.Id;
    }
}

public sealed record UpdateTenantCommand(Guid Id, string Name, string? Address) : IRequest;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().WithMessage("Site adı zorunludur.")
            .MaximumLength(DomainConstants.Tenant.NameMaxLength).WithMessage(DomainConstants.ValidationMessages.TenantNameTooLong);
        RuleFor(command => command.Address)
            .MaximumLength(DomainConstants.Tenant.AddressMaxLength).WithMessage(DomainConstants.ValidationMessages.TenantAddressTooLong);
    }
}

public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand>
{
    private readonly IApplicationDbContext _db;

    public UpdateTenantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Site", request.Id);

        var name = request.Name.Trim();
        var duplicate = await _db.Tenants.AnyAsync(
            item => item.Id != request.Id && item.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new DomainException("Bu site adı zaten kayıtlı.");
        }

        tenant.UpdateDetails(name, request.Address);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SetTenantStatusCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetTenantStatusCommandHandler : IRequestHandler<SetTenantStatusCommand>
{
    private readonly IApplicationDbContext _db;

    public SetTenantStatusCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Site", request.Id);

        if (request.IsActive)
        {
            tenant.Activate();
        }
        else
        {
            tenant.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AddTenantUserCommand(
    Guid TenantId,
    string FullName,
    string Email,
    string Password,
    Guid RoleId) : IRequest<Guid>;

public sealed class AddTenantUserCommandValidator : AbstractValidator<AddTenantUserCommand>
{
    public AddTenantUserCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(DomainConstants.Auth.FullNameMaxLength).WithMessage("Ad soyad en fazla 150 karakter olabilir.");
        RuleFor(command => command.Email).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta girin.");
        RuleFor(command => command.Password).ApplyPasswordRules();
        RuleFor(command => command.RoleId).NotEmpty().WithMessage("Rol seçin.");
    }
}

public sealed class AddTenantUserCommandHandler : IRequestHandler<AddTenantUserCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantUserProvisioner _users;

    public AddTenantUserCommandHandler(IApplicationDbContext db, ITenantUserProvisioner users)
    {
        _db = db;
        _users = users;
    }

    public async Task<Guid> Handle(AddTenantUserCommand request, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AnyAsync(tenant => tenant.Id == request.TenantId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Site", request.TenantId);
        }

        if (await _users.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new DomainException("Bu e-posta zaten kayıtlı.");
        }

        var roleName = await _users.RequireAssignableRoleNameAsync(request.RoleId, request.TenantId, cancellationToken);
        return await _users.CreateAsync(
            request.Email,
            request.Password,
            request.FullName,
            request.TenantId,
            roleName,
            cancellationToken);
    }
}

public static class PasswordRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Şifre en az bir özel karakter içermelidir.");
}
