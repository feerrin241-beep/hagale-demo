namespace Hagale.Application.Contracts;

public interface IPrivateDocumentStorage
{
    Task<StoredDocumentReference> StoreAsync(
        Guid ownerUserId,
        string fileName,
        string contentType,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageObjectKey, CancellationToken cancellationToken = default);

    Task<StoredDocumentContent?> OpenReadAsync(
        string storageObjectKey,
        CancellationToken cancellationToken = default);
}

public sealed record StoredDocumentReference(string StorageObjectKey);

public sealed record StoredDocumentContent(Stream Content, string ContentType);
