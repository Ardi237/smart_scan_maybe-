using System.IO.Compression;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public sealed class AdministrativeScreeningService(
    StoragePaths storagePaths,
    IOptions<ArchivePolicyOptions> archivePolicy,
    ILogger<AdministrativeScreeningService> logger) : IAdministrativeScreeningService
{
    public async Task<AdministrativeScreeningEvaluation> EvaluateAsync(
        Submission submission,
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<CheckResult>
        {
            Pass("METADATA_VALID", "Metadata submission tersedia.", 1),
            Pass("ASSISTANT_MAPPING", $"Absen {submission.AttendanceNumber} dipetakan ke asisten {submission.AssistantCode}.", 2),
            CheckFilename(submission, 3)
        };

        try
        {
            var archivePath = storagePaths.ResolveStoredPath(submission.ArchiveRelativePath);
            using var archive = OpenArchive(archivePath, checks, 4);
            if (archive is null)
            {
                AddSkippedArchiveDependents(checks, 5);
                return new AdministrativeScreeningEvaluation(checks, false);
            }

            var limitsResult = ValidateArchiveLimits(archive, 5);
            checks.Add(limitsResult);
            if (limitsResult.Status != CheckStatuses.Pass)
            {
                AddSkippedExtractionDependents(checks, 6);
                return new AdministrativeScreeningEvaluation(checks, false);
            }

            var extractionResult = await ExtractSafelyAsync(archive, workspacePath, 6, cancellationToken);
            checks.Add(extractionResult);
            if (extractionResult.Status != CheckStatuses.Pass)
            {
                AddSkippedSourceChecks(checks, 7);
                return new AdministrativeScreeningEvaluation(checks, false);
            }

            var csFiles = CheckCsFiles(workspacePath, 7);
            var projectFiles = CheckCsprojFiles(workspacePath, 8);
            checks.Add(csFiles);
            checks.Add(projectFiles);

            return new AdministrativeScreeningEvaluation(
                checks,
                csFiles.Status == CheckStatuses.Pass && projectFiles.Status == CheckStatuses.Pass);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Administrative screening failed for submission {SubmissionId}", submission.Id);
            checks.Add(Error("ADMINISTRATIVE_SCAN", ReasonCodes.AdministrativeScanError, "Pemeriksaan administratif mengalami kesalahan internal.", 99));
            return new AdministrativeScreeningEvaluation(checks, false);
        }
    }

    private static ZipArchive? OpenArchive(string archivePath, ICollection<CheckResult> checks, int sequence)
    {
        try
        {
            var archive = ZipFile.OpenRead(archivePath);
            checks.Add(Pass("ARCHIVE_VALID", "ZIP dapat dibuka dan dibaca.", sequence));
            return archive;
        }
        catch (InvalidDataException)
        {
            checks.Add(Fail("ARCHIVE_VALID", ReasonCodes.ArchiveInvalid, "File .zip tidak valid atau rusak.", sequence));
            return null;
        }
        catch (IOException)
        {
            checks.Add(Fail("ARCHIVE_VALID", ReasonCodes.ArchiveInvalid, "File ZIP tidak dapat dibaca.", sequence));
            return null;
        }
    }

    private CheckResult ValidateArchiveLimits(ZipArchive archive, int sequence)
    {
        var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        if (files.Count > archivePolicy.Value.MaxArchiveEntries)
        {
            return Fail("ARCHIVE_LIMITS", ReasonCodes.ArchiveFileCountLimit, "Jumlah file di dalam ZIP melebihi batas.", sequence);
        }

        long totalUncompressedBytes = 0;
        foreach (var entry in files)
        {
            if (entry.Length > archivePolicy.Value.MaxArchiveEntryBytes)
            {
                return Fail("ARCHIVE_LIMITS", ReasonCodes.ArchiveEntrySizeLimit, "Salah satu file di dalam ZIP melebihi batas ukuran.", sequence);
            }

            try { totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length); }
            catch (OverflowException)
            {
                return Fail("ARCHIVE_LIMITS", ReasonCodes.ArchiveUncompressedSizeLimit, "Ukuran hasil ekstraksi ZIP tidak valid.", sequence);
            }

            if (totalUncompressedBytes > archivePolicy.Value.MaxUncompressedBytes)
            {
                return Fail("ARCHIVE_LIMITS", ReasonCodes.ArchiveUncompressedSizeLimit, "Ukuran hasil ekstraksi ZIP melebihi batas.", sequence);
            }
        }

        return Pass("ARCHIVE_LIMITS", "Jumlah dan ukuran file ZIP berada dalam batas yang diizinkan.", sequence);
    }

    private async Task<CheckResult> ExtractSafelyAsync(ZipArchive archive, string extractionRoot, int sequence, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(extractionRoot);
            var rootWithSeparator = extractionRoot.EndsWith(Path.DirectorySeparatorChar) ? extractionRoot : extractionRoot + Path.DirectorySeparatorChar;
            long totalExtractedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                var destinationPath = ResolveDestinationPath(rootWithSeparator, entry);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var entryStream = entry.Open();
                await using var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true);
                var bytesCopied = await CopyWithLimitAsync(entryStream, destinationStream, archivePolicy.Value.MaxArchiveEntryBytes, cancellationToken);
                totalExtractedBytes = checked(totalExtractedBytes + bytesCopied);
                if (totalExtractedBytes > archivePolicy.Value.MaxUncompressedBytes)
                {
                    return Fail("SAFE_EXTRACTION", ReasonCodes.ArchiveUncompressedSizeLimit, "Ukuran hasil ekstraksi ZIP melebihi batas.", sequence);
                }
            }

            return Pass("SAFE_EXTRACTION", "ZIP berhasil diekstrak ke workspace sementara yang aman.", sequence);
        }
        catch (ArchivePolicyException exception)
        {
            return Fail("SAFE_EXTRACTION", exception.ReasonCode, exception.Message, sequence);
        }
        catch (IOException)
        {
            return Fail("SAFE_EXTRACTION", ReasonCodes.ArchiveExtractionFailed, "ZIP tidak dapat diekstrak dengan aman.", sequence);
        }
    }

    private static string ResolveDestinationPath(string rootWithSeparator, ZipArchiveEntry entry)
    {
        var fullName = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(fullName) || Path.IsPathRooted(fullName) || fullName.Contains(':') || fullName.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            throw new ArchivePolicyException(ReasonCodes.ArchiveUnsafePath, "ZIP memiliki path yang tidak aman.");

        var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixFileType == 0xA000)
            throw new ArchivePolicyException(ReasonCodes.ArchiveSymlinkNotAllowed, "ZIP tidak boleh berisi symbolic link.");

        var destinationPath = Path.GetFullPath(Path.Combine(rootWithSeparator, fullName));
        if (!destinationPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArchivePolicyException(ReasonCodes.ArchiveUnsafePath, "ZIP memiliki path yang keluar dari workspace.");

        return destinationPath;
    }

    private static async Task<long> CopyWithLimitAsync(Stream source, Stream destination, long maximumBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81_920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            copied = checked(copied + read);
            if (copied > maximumBytes)
                throw new ArchivePolicyException(ReasonCodes.ArchiveEntrySizeLimit, "Salah satu file hasil ekstraksi melebihi batas ukuran.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return copied;
    }

    private static CheckResult CheckFilename(Submission submission, int sequence)
    {
        var expectedFileName = submission.Assignment.FilenamePattern
            .Replace("{KODE_ASISTEN}", submission.AssistantCode.Trim(), StringComparison.Ordinal)
            .Replace("{NAMA}", submission.StudentName.Trim().ToUpperInvariant(), StringComparison.Ordinal)
            .Replace("{NIM}", submission.Nim.Trim(), StringComparison.Ordinal);
        return string.Equals(expectedFileName, submission.OriginalFileName, StringComparison.Ordinal)
            ? Pass("FILENAME_VALID", "Nama file sesuai format assignment.", sequence)
            : Fail("FILENAME_VALID", ReasonCodes.FilenameMismatch, $"Nama file harus berupa '{expectedFileName}'.", sequence);
    }

    private static CheckResult CheckCsFiles(string extractionRoot, int sequence) =>
        Directory.EnumerateFiles(extractionRoot, "*.cs", SearchOption.AllDirectories).Any()
            ? Pass("CS_FILE_EXISTS", "File .cs ditemukan.", sequence)
            : Fail("CS_FILE_EXISTS", ReasonCodes.CsFileNotFound, "File .cs tidak ditemukan di dalam ZIP.", sequence);

    private static CheckResult CheckCsprojFiles(string extractionRoot, int sequence)
    {
        var projectFiles = Directory.EnumerateFiles(extractionRoot, "*.csproj", SearchOption.AllDirectories).ToList();
        return projectFiles.Count switch
        {
            1 => Pass("CSPROJ_VALID", "Tepat satu file .csproj ditemukan.", sequence),
            0 => Fail("CSPROJ_VALID", ReasonCodes.CsprojNotFound, "File .csproj tidak ditemukan di dalam ZIP.", sequence),
            _ => Fail("CSPROJ_VALID", ReasonCodes.MultipleCsprojFound, "Lebih dari satu file .csproj ditemukan di dalam ZIP.", sequence)
        };
    }

    private static void AddSkippedArchiveDependents(ICollection<CheckResult> checks, int startSequence)
    {
        checks.Add(Skipped("ARCHIVE_LIMITS", "Pemeriksaan dilewati karena ZIP tidak valid.", startSequence));
        checks.Add(Skipped("SAFE_EXTRACTION", "Pemeriksaan dilewati karena ZIP tidak valid.", startSequence + 1));
        AddSkippedSourceChecks(checks, startSequence + 2);
    }

    private static void AddSkippedExtractionDependents(ICollection<CheckResult> checks, int startSequence)
    {
        checks.Add(Skipped("SAFE_EXTRACTION", "Pemeriksaan dilewati karena batas ZIP gagal.", startSequence));
        AddSkippedSourceChecks(checks, startSequence + 1);
    }

    private static void AddSkippedSourceChecks(ICollection<CheckResult> checks, int startSequence)
    {
        checks.Add(Skipped("CS_FILE_EXISTS", "Pemeriksaan dilewati karena ekstraksi aman gagal.", startSequence));
        checks.Add(Skipped("CSPROJ_VALID", "Pemeriksaan dilewati karena ekstraksi aman gagal.", startSequence + 1));
    }

    private static CheckResult Pass(string checkCode, string message, int sequence) => new() { CheckCode = checkCode, Status = CheckStatuses.Pass, Message = message, Sequence = sequence };
    private static CheckResult Fail(string checkCode, string reasonCode, string message, int sequence) => new() { CheckCode = checkCode, Status = CheckStatuses.Fail, ReasonCode = reasonCode, Message = message, Sequence = sequence };
    private static CheckResult Error(string checkCode, string reasonCode, string message, int sequence) => new() { CheckCode = checkCode, Status = CheckStatuses.Error, ReasonCode = reasonCode, Message = message, Sequence = sequence };
    private static CheckResult Skipped(string checkCode, string message, int sequence) => new() { CheckCode = checkCode, Status = CheckStatuses.Skipped, Message = message, Sequence = sequence };

    private sealed class ArchivePolicyException(string reasonCode, string message) : Exception(message)
    {
        public string ReasonCode { get; } = reasonCode;
    }
}

public sealed record AdministrativeScreeningEvaluation(IReadOnlyList<CheckResult> Checks, bool CanCompile);
