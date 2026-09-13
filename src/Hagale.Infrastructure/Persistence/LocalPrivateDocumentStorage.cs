using Hagale.Application.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hagale.Infrastructure.Persistence;

public sealed class LocalPrivateDocumentStorage(
    IOptions<DocumentStorageOptions> options,
    IHostEnvironment hostEnvironment) : IPrivateDocumentStorage
{
    private const int MaximumFileSizeFloor = 1_024;
    private readonly DocumentStorageOptions _options = options.Value;

    public async Task<StoredDocumentReference> StoreAsync(
        Guid ownerUserId,
        string fileName,
        string contentType,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("El documento debe tener un propietario válido.");
        }

        if (contentLength is <= 0 || contentLength > GetMaximumFileSize())
        {
            throw new InvalidOperationException("El archivo está vacío o supera el tamaño permitido.");
        }

        var extension = GetValidatedExtension(fileName, contentType);
        await ValidateSignatureAsync(content, extension, cancellationToken);
        var objectKey = Path.Combine(ownerUserId.ToString("N"), $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var rootPath = GetRootPath();
        var destinationPath = Path.GetFullPath(Path.Combine(rootPath, objectKey));
        if (!destinationPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta de almacenamiento no es válida.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        try
        {
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true);
            await content.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            return new StoredDocumentReference(objectKey);
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    public Task DeleteAsync(string storageObjectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageObjectKey) || storageObjectKey.Contains("..", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var rootPath = GetRootPath();
        var documentPath = Path.GetFullPath(Path.Combine(rootPath, storageObjectKey));
        if (documentPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(documentPath))
        {
            File.Delete(documentPath);
        }

        return Task.CompletedTask;
    }

    public Task<StoredDocumentContent?> OpenReadAsync(string storageObjectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageObjectKey) || storageObjectKey.Contains("..", StringComparison.Ordinal))
        {
            return Task.FromResult<StoredDocumentContent?>(null);
        }

        var rootPath = GetRootPath();
        var documentPath = Path.GetFullPath(Path.Combine(rootPath, storageObjectKey));
        if (!documentPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(documentPath))
        {
            return Task.FromResult<StoredDocumentContent?>(null);
        }

        var contentType = Path.GetExtension(documentPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        var content = new FileStream(documentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        return Task.FromResult<StoredDocumentContent?>(new StoredDocumentContent(content, contentType));
    }

    private long GetMaximumFileSize() => _options.MaximumFileSizeBytes >= MaximumFileSizeFloor
        ? _options.MaximumFileSizeBytes
        : throw new InvalidOperationException("El tamaño máximo configurado para documentos no es seguro.");

    private string GetRootPath()
    {
        var configuredPath = _options.RootPath?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("La ruta de almacenamiento de documentos no está configurada.");
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(hostEnvironment.ContentRootPath, configuredPath));
    }

    private static string GetValidatedExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var normalizedContentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => null
        };

        if (expectedContentType is null || (normalizedContentType != expectedContentType && normalizedContentType != "application/octet-stream"))
        {
            throw new InvalidOperationException("Solo se aceptan documentos PDF o imágenes JPEG/PNG.");
        }

        return extension;
    }

    private static async Task ValidateSignatureAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("No fue posible validar el contenido del documento.");
        }

        var originalPosition = content.Position;
        var prefix = new byte[8];
        var bytesRead = await content.ReadAsync(prefix, cancellationToken);
        content.Position = originalPosition;

        var valid = extension switch
        {
            ".pdf" => bytesRead >= 5 && prefix.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".jpg" or ".jpeg" => bytesRead >= 3 && prefix.AsSpan(0, 3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => bytesRead >= 8 && prefix.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException("El contenido del archivo no coincide con un documento permitido.");
        }
    }
}
