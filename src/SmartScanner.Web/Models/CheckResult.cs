namespace SmartScanner.Web.Models;

public sealed class CheckResult
{
    public long Id { get; set; }

    public Guid ScreeningRunId { get; set; }

    public string CheckCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ReasonCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? DetailsJson { get; set; }

    public int Sequence { get; set; }

    public ScreeningRun ScreeningRun { get; set; } = null!;
}
