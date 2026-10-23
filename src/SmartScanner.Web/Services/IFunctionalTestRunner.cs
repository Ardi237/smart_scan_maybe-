using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public interface IFunctionalTestRunner
{
    Task<FunctionalExecutionResult> RunAsync(Guid runId, string workspacePath, TestCase testCase, int timeoutSeconds, CancellationToken cancellationToken = default);
}
