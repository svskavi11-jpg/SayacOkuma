namespace Westalife.Application.Common.Interfaces;

/// <summary>Sakin evrakini sunucu klasorune yazar. Veritabaninda yalnizca goreli yol tutulur.</summary>
public interface IResidentDocumentStore
{
    Task<string> SaveAsync(Guid ownerId, string extension, Stream content, CancellationToken cancellationToken);

    Stream OpenRead(string relativePath);

    void Delete(string relativePath);
}
