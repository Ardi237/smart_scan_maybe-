namespace SmartScanner.Web.Models;

public sealed class Assignment
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ScannerProfile { get; set; } = string.Empty;

    public string FilenamePattern { get; set; } = string.Empty;

    public int BuildTimeoutSeconds { get; set; }

    public int RunTimeoutSeconds { get; set; }

    public double NumericTolerance { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}
