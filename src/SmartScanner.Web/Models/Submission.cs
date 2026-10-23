namespace SmartScanner.Web.Models;

public sealed class Submission
{
    public Guid Id { get; set; }

    public long AssignmentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string Nim { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public int AttendanceNumber { get; set; }

    public string AssistantCode { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ArchiveRelativePath { get; set; } = string.Empty;

    public string ArchiveSha256 { get; set; } = string.Empty;

    public long ArchiveBytes { get; set; }

    public string Status { get; set; } = SubmissionStatuses.Received;

    public DateTime UploadedAtUtc { get; set; }

    public Assignment Assignment { get; set; } = null!;

    public ICollection<ScreeningRun> ScreeningRuns { get; set; } = new List<ScreeningRun>();
}
