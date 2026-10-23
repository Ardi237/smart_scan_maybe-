namespace SmartScanner.Web.Configuration;

public sealed class StoragePaths(string dataRoot, string databasePath)
{
    public string DataRoot { get; } = Path.GetFullPath(dataRoot);

    public string DatabasePath { get; } = databasePath;

    public string GetSubmissionDirectory(Guid submissionId) =>
        Path.Combine(DataRoot, "submissions", submissionId.ToString("N"));

    public string GetArchiveRelativePath(Guid submissionId) =>
        Path.Combine("submissions", submissionId.ToString("N"), "original.zip");

    public string GetTemporaryDirectory(Guid runId) =>
        Path.Combine(DataRoot, "temp", runId.ToString("N"));

    public string GetBuildLogRelativePath(Guid runId) =>
        Path.Combine("runs", runId.ToString("N"), "build.log");

    public string ResolveStoredPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Stored paths must be relative to the SmartScanner data directory.");
        }

        var root = DataRoot.EndsWith(Path.DirectorySeparatorChar)
            ? DataRoot
            : DataRoot + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(Path.Combine(DataRoot, relativePath));

        if (!resolvedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The stored path resolves outside the SmartScanner data directory.");
        }

        return resolvedPath;
    }
}
