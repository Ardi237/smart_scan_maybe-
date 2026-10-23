namespace SmartScanner.Web.Services;

public interface IAssistantCodeResolver
{
    Task<string?> ResolveAsync(int attendanceNumber, CancellationToken cancellationToken = default);
}
