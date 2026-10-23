namespace SmartScanner.Web.Models;

public sealed class ScreeningRun
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public Submission Submission { get; set; } = null!;

    public ICollection<CheckResult> CheckResults { get; set; } = new List<CheckResult>();

    public ICollection<FunctionalTestResult> FunctionalTestResults { get; set; } = new List<FunctionalTestResult>();
}
