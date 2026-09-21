namespace Hagale.Infrastructure.Persistence;

/// <summary>
/// Stores private document bytes next to the PostgreSQL data so a Render
/// instance restart cannot remove a driver's submitted documents.
/// </summary>
public sealed class StoredDocumentBlob
{
    private StoredDocumentBlob()
    {
    }

    public StoredDocumentBlob(
        string storageObjectKey,
        Guid ownerUserId,
        string contentType,
        byte[] content,
        DateTimeOffset createdAtUtc)
    {
        StorageObjectKey = storageObjectKey;
        OwnerUserId = ownerUserId;
        ContentType = contentType;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public string StorageObjectKey { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public string ContentType { get; private set; } = null!;
    public byte[] Content { get; private set; } = [];
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
