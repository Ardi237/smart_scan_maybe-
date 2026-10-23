namespace SmartScanner.Web.Configuration;

public sealed class ArchivePolicyOptions
{
    public const string SectionName = "ArchivePolicy";

    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    public int MaxArchiveEntries { get; set; } = 1_000;

    public long MaxArchiveEntryBytes { get; set; } = 25 * 1024 * 1024;

    public long MaxUncompressedBytes { get; set; } = 100 * 1024 * 1024;
}
