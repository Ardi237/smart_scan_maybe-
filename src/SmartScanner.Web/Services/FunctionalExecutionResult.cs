namespace SmartScanner.Web.Services;

public sealed record FunctionalExecutionResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    TimeSpan Duration,
    string StandardOutput,
    string StandardError,
    string? ErrorMessage);
