using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>Sakine ait sozlesme veya tapu kopyasi. Dosyanin kendisi sunucu klasorundedir.</summary>
public class ResidentDocument : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public Guid ApartmentOwnerId { get; private set; }

    public ApartmentOwner ApartmentOwner { get; private set; } = null!;

    public ResidentDocumentType DocumentType { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string FilePath { get; private set; } = string.Empty;

    private ResidentDocument() { }

    public ResidentDocument(Guid apartmentOwnerId, ResidentDocumentType documentType, string fileName, string filePath)
    {
        if (apartmentOwnerId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.ResidentDocument.OwnerNotFound);
        }

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(filePath))
        {
            throw new DomainException(DomainConstants.ResidentDocument.FileRequired);
        }

        ApartmentOwnerId = apartmentOwnerId;
        DocumentType = documentType;
        FileName = fileName.Trim();
        FilePath = filePath.Trim();
    }
}
