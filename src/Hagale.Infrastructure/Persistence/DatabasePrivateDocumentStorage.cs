using Hagale.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hagale.Infrastructure.Persistence;

public sealed class DatabasePrivateDocumentStorage(
    HagaleDbContext database,
    IOptions<DocumentStorageOptions> options,
    TimeProvider timeProvider) : IPrivateDocumentStorage
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
        await using var bufferedContent = new MemoryStream(capacity: checked((int)contentLength));
        await content.CopyToAsync(bufferedContent, cancellationToken);
        if (bufferedContent.Length != contentLength)
        {
            throw new InvalidOperationException("El archivo no pudo leerse completamente.");
        }

        var objectKey = $"{ownerUserId:N}/{Guid.NewGuid():N}{extension}";
        database.StoredDocumentBlobs.Add(new StoredDocumentBlob(
            objectKey,
            ownerUserId,
            NormalizeContentType(contentType, extension),
            bufferedContent.ToArray(),
            timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
        return new StoredDocumentReference(objectKey);
    }

    public async Task DeleteAsync(string storageObjectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageObjectKey) || storageObjectKey.Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        var storedDocument = await database.StoredDocumentBlobs
            .SingleOrDefaultAsync(document => document.StorageObjectKey == storageObjectKey, cancellationToken);
        if (storedDocument is null)
        {
            return;
        }

        database.StoredDocumentBlobs.Remove(storedDocument);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredDocumentContent?> OpenReadAsync(
        string storageObjectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageObjectKey) || storageObjectKey.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var storedDocument = await database.StoredDocumentBlobs
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.StorageObjectKey == storageObjectKey, cancellationToken);
        return storedDocument is null
            ? null
            : new StoredDocumentContent(
                new MemoryStream(storedDocument.Content, writable: false),
                storedDocument.ContentType);
    }

    private long GetMaximumFileSize() => _options.MaximumFileSizeBytes >= MaximumFileSizeFloor
        ? _options.MaximumFileSizeBytes
        : throw new InvalidOperationException("El tamaño máximo configurado para documentos no es seguro.");

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

    private static string NormalizeContentType(string contentType, string extension) =>
        contentType.Split(';', 2)[0].Trim().ToLowerInvariant() switch
        {
            "application/pdf" => "application/pdf",
            "image/jpeg" => "image/jpeg",
            "image/png" => "image/png",
            _ when extension == ".pdf" => "application/pdf",
            _ when extension is ".jpg" or ".jpeg" => "image/jpeg",
            _ when extension == ".png" => "image/png",
            _ => "application/octet-stream"
        };

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
