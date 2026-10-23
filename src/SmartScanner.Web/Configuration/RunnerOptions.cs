namespace SmartScanner.Web.Configuration;

public sealed class RunnerOptions
{
    public const string SectionName = "Runner";

    public string ImageName { get; set; } = "smartscanner-runner-poc:1";

    public int BuildTimeoutSeconds { get; set; } = 30;

    public string MemoryLimit { get; set; } = "256m";

    public string CpuLimit { get; set; } = "1";

    public int ProcessLimit { get; set; } = 64;
}
