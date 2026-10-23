using Microsoft.AspNetCore.Http;

namespace SmartScanner.Web.Services;

public sealed class SubmissionUploadRequest
{
    public long AssignmentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string Nim { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public int AttendanceNumber { get; init; }
    public IFormFile? Archive { get; init; }
}

public sealed record SubmissionUploadResult(bool Succeeded, Guid? SubmissionId, string? ReasonCode, string Message);
