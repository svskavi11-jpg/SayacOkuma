using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Roles;

public sealed record RoleAssignmentListItem(
    Guid Id,
    Guid ApartmentId,
    string PersonName,
    RoleType RoleType,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal ExemptionRate,
    DateOnly? BoardDecisionDate,
    bool IsOpen,
    bool IsCancelled = false,
    DateTimeOffset? CancelledAt = null,
    RolePaymentType PaymentType = RolePaymentType.Oran,
    decimal? FixedAmount = null,
    RolePeriodType? PeriodType = null);

public sealed record GetRoleAssignmentsQuery(Guid ApartmentId) : IRequest<IReadOnlyList<RoleAssignmentListItem>>;

public sealed class GetRoleAssignmentsQueryHandler
    : IRequestHandler<GetRoleAssignmentsQuery, IReadOnlyList<RoleAssignmentListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetRoleAssignmentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleAssignmentListItem>> Handle(
        GetRoleAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.Apartments.AsNoTracking()
            .AnyAsync(a => a.Id == request.ApartmentId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(DomainConstants.RoleAssignment.ApartmentNotFound);
        }

        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);

        return await _db.RoleAssignments.AsNoTracking()
            .Where(r => r.ApartmentId == request.ApartmentId)
            .OrderByDescending(r => r.StartDate)
            .ThenByDescending(r => r.CreatedOn)
            .Select(r => new RoleAssignmentListItem(
                r.Id,
                r.ApartmentId,
                r.PersonName,
                r.RoleType,
                r.StartDate,
                r.EndDate,
                r.ExemptionRate,
                r.BoardDecisionDate,
                r.EndDate == null && !r.IsCancelled,
                r.IsCancelled,
                r.CancelledAt,
                r.PaymentType,
                r.FixedAmount,
                r.PeriodType))
            .ToListAsync(cancellationToken);
    }
}

public sealed record AssignRoleCommand(
    Guid ApartmentId,
    string PersonName,
    RoleType RoleType,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal ExemptionRate,
    DateOnly? BoardDecisionDate,
    RolePaymentType PaymentType = RolePaymentType.Oran,
    decimal? FixedAmount = null,
    RolePeriodType? PeriodType = null) : IRequest<Guid>;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();
        RuleFor(c => c.PersonName)
            .NotEmpty()
            .WithMessage(DomainConstants.RoleAssignment.PersonNameRequired)
            .MaximumLength(DomainConstants.RoleAssignment.PersonNameMaxLength)
            .WithMessage(DomainConstants.RoleAssignment.PersonNameTooLong);
        RuleFor(c => c.RoleType).IsInEnum();
        RuleFor(c => c.PaymentType)
            .IsInEnum()
            .WithMessage(DomainConstants.RoleAssignment.PaymentTypeInvalid);
        RuleFor(c => c.StartDate).NotEmpty();
        RuleFor(c => c.EndDate)
            .GreaterThanOrEqualTo(c => c.StartDate)
            .WithMessage(DomainConstants.RoleAssignment.DateInvalid)
            .When(c => c.EndDate.HasValue);
        RuleFor(c => c.ExemptionRate)
            .InclusiveBetween(0m, 1m)
            .WithMessage(DomainConstants.RoleAssignment.RateInvalid)
            .When(c => c.PaymentType == RolePaymentType.Oran);
        RuleFor(c => c.FixedAmount)
            .NotNull()
            .WithMessage(DomainConstants.RoleAssignment.FixedAmountInvalid)
            .GreaterThan(0m)
            .WithMessage(DomainConstants.RoleAssignment.FixedAmountInvalid)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.RoleAssignment.FixedAmountInvalid)
            .When(c => c.PaymentType == RolePaymentType.SabitTutar);
        RuleFor(c => c.PeriodType)
            .NotNull()
            .WithMessage(DomainConstants.RoleAssignment.PeriodTypeRequired)
            .IsInEnum()
            .WithMessage(DomainConstants.RoleAssignment.PeriodTypeRequired)
            .When(c => c.PaymentType == RolePaymentType.SabitTutar);
    }
}

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public AssignRoleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var exists = await _db.Apartments.AnyAsync(a => a.Id == request.ApartmentId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(DomainConstants.RoleAssignment.ApartmentNotFound);
        }

        var created = RoleAssignment.Create(
            request.ApartmentId,
            request.PersonName,
            request.RoleType,
            request.StartDate,
            request.EndDate,
            request.ExemptionRate,
            request.BoardDecisionDate,
            request.PaymentType,
            request.FixedAmount,
            request.PeriodType);

        // Görev mahsubu (Muafiyet % > 0 veya Sabit Tutar > 0) üretilecekse Ayarlar'dan bütçe kalemi seçilmiş olması zorunludur.
        if (created.HasCompensation)
        {
            var settings = await _db.AppSettings.AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var budgetItemId = settings?.ManagementExpenseBudgetItemId;
            if (!budgetItemId.HasValue || budgetItemId.Value == Guid.Empty)
            {
                throw new DomainException(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured);
            }

            var budgetItemExists = await _db.BudgetItems.AsNoTracking()
                .AnyAsync(b => b.Id == budgetItemId.Value, cancellationToken);
            if (!budgetItemExists)
            {
                throw new DomainException(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured);
            }
        }

        var existing = await _db.RoleAssignments
            .Where(r => r.ApartmentId == request.ApartmentId)
            .ToListAsync(cancellationToken);

        RoleAssignment.Place(existing, created);
        _db.RoleAssignments.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        await WaiverOffsetSyncService.SyncAllAsync(
            _db,
            cancellationToken,
            requireBudgetItemWhenCompensated: created.HasCompensation);

        return created.Id;
    }
}

public sealed record CloseRoleAssignmentCommand(Guid Id, DateOnly EndDate) : IRequest;

public sealed class CloseRoleAssignmentCommandValidator : AbstractValidator<CloseRoleAssignmentCommand>
{
    public CloseRoleAssignmentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EndDate).NotEmpty();
    }
}

public sealed class CloseRoleAssignmentCommandHandler : IRequestHandler<CloseRoleAssignmentCommand>
{
    private readonly IApplicationDbContext _db;

    public CloseRoleAssignmentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(CloseRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _db.RoleAssignments
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.RoleAssignment.NotFound);

        var overlaps = await _db.RoleAssignments
            .Where(r => r.ApartmentId == assignment.ApartmentId && r.Id != assignment.Id && !r.IsCancelled)
            .ToListAsync(cancellationToken);

        assignment.Close(request.EndDate);
        if (overlaps.Any(r => RoleAssignment.RangesOverlap(
                r.StartDate, r.EndDate, assignment.StartDate, assignment.EndDate)))
        {
            throw new DomainException(DomainConstants.RoleAssignment.Overlap);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);
    }
}

public sealed record CancelRoleAssignmentCommand(Guid Id) : IRequest<CancelRoleAssignmentResult>;

public sealed record CancelRoleAssignmentResult(
    Guid Id,
    bool IsCancelled,
    DateTimeOffset CancelledAt,
    int CancelledWaiverCount);

public sealed class CancelRoleAssignmentCommandHandler
    : IRequestHandler<CancelRoleAssignmentCommand, CancelRoleAssignmentResult>
{
    private readonly IApplicationDbContext _db;

    public CancelRoleAssignmentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CancelRoleAssignmentResult> Handle(
        CancelRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _db.RoleAssignments
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.RoleAssignment.NotFound);

        if (assignment.IsCancelled)
        {
            throw new DomainException(DomainConstants.RoleAssignment.AlreadyCancelled);
        }

        var waivers = await _db.WaiverOffsets
            .Where(w => w.RoleAssignmentId == assignment.Id && !w.IsCancelled)
            .ToListAsync(cancellationToken);

        if (waivers.Count > 0)
        {
            var years = waivers.Select(w => w.OffsetDate.Year).Distinct().ToList();
            var isLocked = await _db.BudgetPeriods
                .AsNoTracking()
                .AnyAsync(p => years.Contains(p.Year) && p.IsLocked, cancellationToken);

            if (isLocked)
            {
                throw new DomainException(DomainConstants.RoleAssignment.CancelBlockedByLockedPeriod);
            }
        }

        var now = DateTimeOffset.UtcNow;
        assignment.Cancel(now);

        foreach (var waiver in waivers)
        {
            waiver.Cancel(now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelRoleAssignmentResult(
            assignment.Id,
            assignment.IsCancelled,
            assignment.CancelledAt!.Value,
            waivers.Count);
    }
}
