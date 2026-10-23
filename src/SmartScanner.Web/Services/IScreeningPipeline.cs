namespace SmartScanner.Web.Services;

public interface IScreeningPipeline
{
    Task RunAsync(Guid submissionId, CancellationToken cancellationToken = default);
}
