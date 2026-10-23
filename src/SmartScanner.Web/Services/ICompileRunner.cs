namespace SmartScanner.Web.Services;

public interface ICompileRunner
{
    Task<CompileResult> BuildAsync(Guid runId, string workspacePath, CancellationToken cancellationToken = default);
}
