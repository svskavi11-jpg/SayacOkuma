using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Residents.Documents;

public sealed record ResidentDocumentItem(
    Guid Id,
    Guid ApartmentOwnerId,
    string OwnerName,
    ResidentType ResidentType,
    ResidentDocumentType DocumentType,
    string FileName,
    DateTimeOffset UploadedOn);

public sealed record GetResidentDocumentsQuery(Guid ApartmentId) : IRequest<IReadOnlyList<ResidentDocumentItem>>;

public sealed class GetResidentDocumentsQueryHandler
    : IRequestHandler<GetResidentDocumentsQuery, IReadOnlyList<ResidentDocumentItem>>
{
    private readonly IApplicationDbContext _db;

    public GetResidentDocumentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResidentDocumentItem>> Handle(
        GetResidentDocumentsQuery request, CancellationToken cancellationToken)
    {
        return await _db.ResidentDocuments.AsNoTracking()
            .Where(d => d.ApartmentOwner.ApartmentId == request.ApartmentId)
            .OrderByDescending(d => d.CreatedOn)
            .Select(d => new ResidentDocumentItem(
                d.Id,
                d.ApartmentOwnerId,
                d.ApartmentOwner.OwnerName,
                d.ApartmentOwner.ResidentType,
                d.DocumentType,
                d.FileName,
                d.CreatedOn))
            .ToListAsync(cancellationToken);
    }
}

public sealed record UploadResidentDocumentCommand(
    Guid ApartmentOwnerId,
    ResidentDocumentType DocumentType,
    string FileName,
    byte[] Content) : IRequest<ResidentDocumentItem>;

public sealed class UploadResidentDocumentCommandValidator : AbstractValidator<UploadResidentDocumentCommand>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png",
    };

    public UploadResidentDocumentCommandValidator()
    {
        RuleFor(c => c.ApartmentOwnerId).NotEmpty();
        RuleFor(c => c.Content)
            .NotEmpty().WithMessage(DomainConstants.ResidentDocument.FileRequired)
            .Must(c => c is not null && c.Length <= DomainConstants.ResidentDocument.MaxBytes)
            .WithMessage(DomainConstants.ResidentDocument.FileTooLarge);
        RuleFor(c => c.FileName)
            .Must(name => Allowed.Contains(Path.GetExtension(name ?? string.Empty)))
            .WithMessage(DomainConstants.ResidentDocument.FileTypeInvalid);
    }
}

public sealed class UploadResidentDocumentCommandHandler
    : IRequestHandler<UploadResidentDocumentCommand, ResidentDocumentItem>
{
    private readonly IApplicationDbContext _db;
    private readonly IResidentDocumentStore _store;

    public UploadResidentDocumentCommandHandler(IApplicationDbContext db, IResidentDocumentStore store)
    {
        _db = db;
        _store = store;
    }

    public async Task<ResidentDocumentItem> Handle(
        UploadResidentDocumentCommand request, CancellationToken cancellationToken)
    {
        var owner = await _db.ApartmentOwners.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.ApartmentOwnerId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.ResidentDocument.OwnerNotFound);

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        await using var stream = new MemoryStream(request.Content);
        var relativePath = await _store.SaveAsync(owner.Id, extension, stream, cancellationToken);
        var safeName = Path.GetFileName(request.FileName.Trim());
        if (safeName.Length > DomainConstants.ResidentDocument.FileNameMaxLength)
        {
            safeName = safeName[..DomainConstants.ResidentDocument.FileNameMaxLength];
        }

        var document = new ResidentDocument(owner.Id, request.DocumentType, safeName, relativePath);
        _db.ResidentDocuments.Add(document);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _store.Delete(relativePath);
            throw;
        }

        return new ResidentDocumentItem(
            document.Id,
            owner.Id,
            owner.OwnerName,
            owner.ResidentType,
            document.DocumentType,
            document.FileName,
            document.CreatedOn);
    }
}

public sealed record ResidentDocumentFile(string FileName, string ContentType, Stream Content);

public sealed record OpenResidentDocumentQuery(Guid DocumentId) : IRequest<ResidentDocumentFile>;

public sealed class OpenResidentDocumentQueryHandler : IRequestHandler<OpenResidentDocumentQuery, ResidentDocumentFile>
{
    private readonly IApplicationDbContext _db;
    private readonly IResidentDocumentStore _store;

    public OpenResidentDocumentQueryHandler(IApplicationDbContext db, IResidentDocumentStore store)
    {
        _db = db;
        _store = store;
    }

    public async Task<ResidentDocumentFile> Handle(OpenResidentDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await _db.ResidentDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.ResidentDocument.NotFound);

        var contentType = Path.GetExtension(document.FileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/pdf",
        };
        return new ResidentDocumentFile(document.FileName, contentType, _store.OpenRead(document.FilePath));
    }
}

public sealed record DeleteResidentDocumentCommand(Guid DocumentId) : IRequest;

public sealed class DeleteResidentDocumentCommandHandler : IRequestHandler<DeleteResidentDocumentCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IResidentDocumentStore _store;

    public DeleteResidentDocumentCommandHandler(IApplicationDbContext db, IResidentDocumentStore store)
    {
        _db = db;
        _store = store;
    }

    public async Task Handle(DeleteResidentDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _db.ResidentDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.ResidentDocument.NotFound);

        var path = document.FilePath;
        _db.ResidentDocuments.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);
        _store.Delete(path);
    }
}
