using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Roles;

public sealed record ListStaffQuery : IRequest<IReadOnlyList<StaffAccountItem>>;

public sealed class ListStaffQueryHandler : IRequestHandler<ListStaffQuery, IReadOnlyList<StaffAccountItem>>
{
    private readonly IAccountSecurity _accounts;
    private readonly ITenantContext _tenant;

    public ListStaffQueryHandler(IAccountSecurity accounts, ITenantContext tenant)
    {
        _accounts = accounts;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<StaffAccountItem>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Personel listesi için bir site seçilmelidir.");
        }

        return _accounts.ListStaffAsync(cancellationToken);
    }
}
