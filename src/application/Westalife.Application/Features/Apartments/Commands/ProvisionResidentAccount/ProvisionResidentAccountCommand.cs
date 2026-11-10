using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Admin.Tenants;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.ProvisionResidentAccount;

public sealed record ProvisionResidentAccountCommand(
    Guid ApartmentId,
    string FullName,
    string Email,
    string Password) : IRequest<Guid>;

public sealed class ProvisionResidentAccountCommandValidator : AbstractValidator<ProvisionResidentAccountCommand>
{
    public ProvisionResidentAccountCommandValidator()
    {
        RuleFor(command => command.ApartmentId).NotEmpty();
        RuleFor(command => command.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(DomainConstants.Auth.FullNameMaxLength).WithMessage("Ad soyad en fazla 150 karakter olabilir.");
        RuleFor(command => command.Email).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta girin.");
        RuleFor(command => command.Password).ApplyPasswordRules();
    }
}

public sealed class ProvisionResidentAccountCommandHandler : IRequestHandler<ProvisionResidentAccountCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITenantUserProvisioner _users;

    public ProvisionResidentAccountCommandHandler(
        IApplicationDbContext db,
        ITenantContext tenant,
        ITenantUserProvisioner users)
    {
        _db = db;
        _tenant = tenant;
        _users = users;
    }

    public async Task<Guid> Handle(ProvisionResidentAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Sakin hesabı açmak için bir site seçilmelidir.");
        }

        var apartment = await _db.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire", request.ApartmentId);

        if (apartment.TenantId != _tenant.TenantId)
        {
            throw new NotFoundException("Daire", request.ApartmentId);
        }

        return await _users.ProvisionResidentAsync(
            request.Email,
            request.Password,
            request.FullName,
            _tenant.TenantId,
            apartment.Id,
            cancellationToken);
    }
}
