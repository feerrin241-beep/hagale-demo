namespace Hagale.Infrastructure.Persistence;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string RootPath { get; init; } = "App_Data/private-documents";
    public long MaximumFileSizeBytes { get; init; } = 5_242_880;
}
