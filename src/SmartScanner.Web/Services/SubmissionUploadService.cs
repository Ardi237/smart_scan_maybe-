using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public sealed class SubmissionUploadService(
    SmartScannerDbContext dbContext,
    StoragePaths storagePaths,
    IOptions<ArchivePolicyOptions> archivePolicy,
    IAssistantCodeResolver assistantCodeResolver,
    IScreeningPipeline screeningPipeline,
    ILogger<SubmissionUploadService> logger) : ISubmissionUploadService
{
    public async Task<SubmissionUploadResult> CreateAsync(SubmissionUploadRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var assistantCode = await assistantCodeResolver.ResolveAsync(request.AttendanceNumber, cancellationToken);
        if (assistantCode is null)
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.AttendanceNotMapped, "Nomor absen belum memiliki pemetaan kode asisten aktif di General Settings.");
        }

        var submissionId = Guid.NewGuid();
        var submissionDirectory = storagePaths.GetSubmissionDirectory(submissionId);
        var archiveRelativePath = storagePaths.GetArchiveRelativePath(submissionId);
        var archivePath = storagePaths.ResolveStoredPath(archiveRelativePath);

        try
        {
            Directory.CreateDirectory(submissionDirectory);
            var hash = await StoreArchiveAsync(request.Archive!, archivePath, cancellationToken);

            var submission = new Submission
            {
                Id = submissionId,
                AssignmentId = request.AssignmentId,
                StudentName = request.StudentName.Trim(),
                Nim = request.Nim.Trim(),
                ClassName = request.ClassName.Trim(),
                AttendanceNumber = request.AttendanceNumber,
                AssistantCode = assistantCode,
                OriginalFileName = Path.GetFileName(request.Archive!.FileName),
                ArchiveRelativePath = archiveRelativePath,
                ArchiveSha256 = hash,
                ArchiveBytes = request.Archive.Length,
                Status = SubmissionStatuses.Received,
                UploadedAtUtc = DateTime.UtcNow
            };

            dbContext.Submissions.Add(submission);
            await dbContext.SaveChangesAsync(cancellationToken);
            await screeningPipeline.RunAsync(submission.Id, cancellationToken);

            logger.LogInformation("Stored submission {SubmissionId} with SHA-256 {Sha256}", submission.Id, hash);
            return new SubmissionUploadResult(true, submission.Id, null, "Submission berhasil disimpan dan diproses oleh screening pipeline.");
        }
        catch (OperationCanceledException)
        {
            CleanupStorage(submissionDirectory);
            throw;
        }
        catch (Exception exception)
        {
            CleanupStorage(submissionDirectory);
            logger.LogError(exception, "Could not store submission {SubmissionId}", submissionId);
            return new SubmissionUploadResult(false, null, ReasonCodes.AdministrativeScanError, "Submission tidak dapat disimpan dengan aman.");
        }
    }

    private async Task<SubmissionUploadResult?> ValidateAsync(SubmissionUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.AssignmentId <= 0 || string.IsNullOrWhiteSpace(request.StudentName) || string.IsNullOrWhiteSpace(request.Nim) || string.IsNullOrWhiteSpace(request.ClassName) || request.AttendanceNumber <= 0 || request.Archive is null)
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.MetadataIncomplete, "Metadata submission dan file ZIP wajib diisi.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Nim.Trim(), "^[0-9]{8,20}$"))
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.MetadataIncomplete, "NIM harus berupa 8 sampai 20 digit angka.");
        }

        if (!string.Equals(Path.GetExtension(request.Archive.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.InvalidFileExtension, "Hanya file dengan ekstensi .zip yang dapat diunggah.");
        }

        if (request.Archive.Length == 0)
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.ArchiveEmpty, "File ZIP tidak boleh kosong.");
        }

        if (request.Archive.Length > archivePolicy.Value.MaxUploadBytes)
        {
            return new SubmissionUploadResult(false, null, ReasonCodes.ArchiveTooLarge, "Ukuran file ZIP melebihi batas upload.");
        }

        var assignmentExists = await dbContext.Assignments.AnyAsync(assignment => assignment.Id == request.AssignmentId && assignment.IsActive, cancellationToken);
        return assignmentExists
            ? null
            : new SubmissionUploadResult(false, null, ReasonCodes.AssignmentNotFound, "Assignment tidak ditemukan atau tidak aktif.");
    }

    private static async Task<string> StoreArchiveAsync(IFormFile archive, string archivePath, CancellationToken cancellationToken)
    {
        await using (var source = archive.OpenReadStream())
        await using (var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        await using var storedArchive = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        var hash = await SHA256.HashDataAsync(storedArchive, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void CleanupStorage(string submissionDirectory)
    {
        if (Directory.Exists(submissionDirectory))
        {
            Directory.Delete(submissionDirectory, recursive: true);
        }
    }
}
