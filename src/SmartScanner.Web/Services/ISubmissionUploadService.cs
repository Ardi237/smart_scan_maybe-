namespace SmartScanner.Web.Services;

public interface ISubmissionUploadService
{
    Task<SubmissionUploadResult> CreateAsync(SubmissionUploadRequest request, CancellationToken cancellationToken = default);
}
