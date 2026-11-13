using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services;

namespace Westalife.Application.Features.Payments;

internal static class ReceiptNumberAllocator
{
    public static async Task<List<string>> LoadAsync(
        IApplicationDbContext db, Guid? exceptPaymentId, CancellationToken cancellationToken)
    {
        return await db.Payments
            .Where(p => p.ReceiptNumber != null && (exceptPaymentId == null || p.Id != exceptPaymentId))
            .Select(p => p.ReceiptNumber!)
            .ToListAsync(cancellationToken);
    }

    public static string Allocate(ICollection<string> existing, int year, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var number = ReceiptNumberSequence.Normalize(requested);
            if (existing.Contains(number, StringComparer.Ordinal))
            {
                throw new DomainException(DomainConstants.Payment.ReceiptNumberDuplicate);
            }

            existing.Add(number);
            return number;
        }

        var next = ReceiptNumberSequence.Next(existing, year);
        existing.Add(next);
        return next;
    }
}
