using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Application.Features.Budget.Commands.BudgetPeriod;

public sealed record BudgetPeriodStatus(int Year, bool IsLocked, DateTimeOffset? LockedOn);

public sealed record GetBudgetPeriodQuery(int Year) : IRequest<BudgetPeriodStatus>;

public sealed class GetBudgetPeriodQueryValidator : AbstractValidator<GetBudgetPeriodQuery>
{
    public GetBudgetPeriodQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetBudgetPeriodQueryHandler : IRequestHandler<GetBudgetPeriodQuery, BudgetPeriodStatus>
{
    private readonly IApplicationDbContext _db;

    public GetBudgetPeriodQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BudgetPeriodStatus> Handle(GetBudgetPeriodQuery request, CancellationToken cancellationToken)
    {
        var period = await _db.BudgetPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Year == request.Year, cancellationToken);

        return period is null
            ? new BudgetPeriodStatus(request.Year, false, null)
            : new BudgetPeriodStatus(period.Year, period.IsLocked, period.LockedOn);
    }
}

public sealed record SetBudgetPeriodLockCommand(int Year, bool Locked) : IRequest<BudgetPeriodStatus>;

public sealed class SetBudgetPeriodLockCommandValidator : AbstractValidator<SetBudgetPeriodLockCommand>
{
    public SetBudgetPeriodLockCommandValidator()
    {
        RuleFor(c => c.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class SetBudgetPeriodLockCommandHandler : IRequestHandler<SetBudgetPeriodLockCommand, BudgetPeriodStatus>
{
    private readonly IApplicationDbContext _db;

    public SetBudgetPeriodLockCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BudgetPeriodStatus> Handle(SetBudgetPeriodLockCommand request, CancellationToken cancellationToken)
    {
        var period = await _db.BudgetPeriods.FirstOrDefaultAsync(p => p.Year == request.Year, cancellationToken);
        if (period is null)
        {
            period = new Westalife.Domain.Entities.BudgetPeriod(request.Year);
            _db.BudgetPeriods.Add(period);
        }

        if (request.Locked)
        {
            period.Lock();
        }
        else
        {
            period.Unlock();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new BudgetPeriodStatus(period.Year, period.IsLocked, period.LockedOn);
    }
}
