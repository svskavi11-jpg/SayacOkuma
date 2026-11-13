using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Payments.Commands.DeletePayment;
using Westalife.Application.Features.Payments.Commands.RecordPayment;
using Westalife.Application.Features.Payments.Queries.GetPaymentReceipt;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class PaymentReceiptTests
{
    [Fact]
    public async Task RecordPayment_AssignsReceipt_AndPdfStartsWithHeader()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var db = provider.GetRequiredService<IApplicationDbContext>();
        var apartmentId = await db.Apartments.Select(a => a.Id).FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var created = new List<Guid>();

        try
        {
            var first = await mediator.Send(new RecordPaymentCommand(apartmentId, today, 12.50m, "Makbuz testi", null));
            var second = await mediator.Send(new RecordPaymentCommand(apartmentId, today, 3m, "Makbuz testi 2", null));
            created.Add(first.PaymentId);
            created.Add(second.PaymentId);

            var numbers = await db.Payments
                .Where(p => p.Id == first.PaymentId || p.Id == second.PaymentId)
                .Select(p => new { p.Id, p.ReceiptNumber })
                .ToListAsync();
            var firstNumber = numbers.Single(p => p.Id == first.PaymentId).ReceiptNumber;
            var secondNumber = numbers.Single(p => p.Id == second.PaymentId).ReceiptNumber;
            Assert.Matches(@"^\d{4}-\d{4,}$", firstNumber);
            Assert.NotEqual(firstNumber, secondNumber);

            var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new RecordPaymentCommand(
                    apartmentId, today, 1m, "Mukerrer", null, ReceiptNumber: firstNumber)));
            Assert.Contains(DomainConstants.Payment.ReceiptNumberDuplicate, duplicate.Message);

            var file = await mediator.Send(new GetPaymentReceiptQuery(first.PaymentId));
            Assert.Equal($"Makbuz-{firstNumber}.pdf", file.FileName);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
        }
        finally
        {
            foreach (var id in created)
            {
                await mediator.Send(new DeletePaymentCommand(id));
            }
        }
    }
}
