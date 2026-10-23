namespace SmartScanner.Web.Models;

public sealed class AssistantAssignmentRule
{
    public long Id { get; set; }
    public int MinimumAttendanceNumber { get; set; }
    public int MaximumAttendanceNumber { get; set; }
    public string AssistantCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
