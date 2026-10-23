namespace SmartScanner.Web.Models;

public sealed class FunctionalTestResult
{
    public long Id { get; set; }
    public Guid ScreeningRunId { get; set; }
    public long TestCaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string StandardInput { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public string ExpectedValuesJson { get; set; } = string.Empty;
    public string? ActualValuesJson { get; set; }
    public int? ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public long DurationMilliseconds { get; set; }
    public int Sequence { get; set; }

    public ScreeningRun ScreeningRun { get; set; } = null!;
    public TestCase TestCase { get; set; } = null!;
}
