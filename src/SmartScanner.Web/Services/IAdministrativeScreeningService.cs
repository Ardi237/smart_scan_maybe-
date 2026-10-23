using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public interface IAdministrativeScreeningService
{
    Task<AdministrativeScreeningEvaluation> EvaluateAsync(
        Submission submission,
        string workspacePath,
        CancellationToken cancellationToken = default);
}
