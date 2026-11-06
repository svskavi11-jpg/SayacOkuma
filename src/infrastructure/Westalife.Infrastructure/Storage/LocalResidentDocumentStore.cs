using Westalife.Application.Common.Interfaces;

namespace Westalife.Infrastructure.Storage;

public sealed class LocalResidentDocumentStore : IResidentDocumentStore
{
    private readonly string _root;

    public LocalResidentDocumentStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Guid ownerId, string extension, Stream content, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_root, ownerId.ToString("N"));
        Directory.CreateDirectory(folder);
        var fileName = Guid.NewGuid().ToString("N") + extension;
        var fullPath = Path.Combine(folder, fileName);
        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);
        return Path.Combine(ownerId.ToString("N"), fileName);
    }

    public Stream OpenRead(string relativePath) => File.OpenRead(Resolve(relativePath));

    public void Delete(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private string Resolve(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Geçersiz evrak yolu.");
        }

        return fullPath;
    }
}
