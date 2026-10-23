using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public interface IFunctionalTestingService
{
    Task<IReadOnlyList<FunctionalTestResult>> RunAsync(Guid runId, Assignment assignment, string workspacePath, CancellationToken cancellationToken = default);
}
