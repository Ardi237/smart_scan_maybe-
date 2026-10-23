namespace SmartScanner.Web.Models;

public sealed class TestCase
{
    public long Id { get; set; }

    public long AssignmentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StandardInput { get; set; } = string.Empty;

    public string ExpectedValuesJson { get; set; } = string.Empty;

    public string Comparator { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public bool IsActive { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
