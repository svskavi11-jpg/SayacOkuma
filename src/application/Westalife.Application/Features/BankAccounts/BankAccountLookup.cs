using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.BankAccounts;

internal static class BankAccountLookup
{
    public static async Task<Guid?> ResolveAsync(
        IApplicationDbContext db,
        Guid? bankAccountId,
        Guid? currentBankAccountId,
        CancellationToken cancellationToken)
    {
        if (bankAccountId is null || bankAccountId == Guid.Empty)
        {
            return null;
        }

        var account = await db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == bankAccountId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankAccount.NotFound);

        if (!account.IsActive && bankAccountId != currentBankAccountId)
        {
            throw new DomainException(DomainConstants.BankAccount.Inactive);
        }

        return account.Id;
    }
}