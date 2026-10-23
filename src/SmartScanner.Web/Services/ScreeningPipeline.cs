using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public sealed class ScreeningPipeline(
    SmartScannerDbContext dbContext,
    StoragePaths storagePaths,
    IAdministrativeScreeningService administrativeScreening,
    ICompileRunner compileRunner,
    IFunctionalTestingService functionalTesting,
    ILogger<ScreeningPipeline> logger) : IScreeningPipeline
{
    public async Task RunAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await dbContext.Submissions
            .Include(item => item.Assignment)
                .ThenInclude(assignment => assignment.TestCases)
            .SingleAsync(item => item.Id == submissionId, cancellationToken);
        var run = new ScreeningRun
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Stage = "FULL",
            Status = SubmissionStatuses.Processing,
            StartedAtUtc = DateTime.UtcNow
        };
        dbContext.ScreeningRuns.Add(run);
        submission.Status = SubmissionStatuses.Processing;
        await dbContext.SaveChangesAsync(cancellationToken);

        var workspacePath = storagePaths.GetTemporaryDirectory(run.Id);
        try
        {
            var administrative = await administrativeScreening.EvaluateAsync(submission, workspacePath, cancellationToken);
            foreach (var check in administrative.Checks)
            {
                run.CheckResults.Add(check);
            }

            if (administrative.CanCompile)
            {
                var compileResult = await compileRunner.BuildAsync(run.Id, workspacePath, cancellationToken);
                var buildCheck = await ToBuildCheckAsync(run.Id, compileResult, cancellationToken);
                run.CheckResults.Add(buildCheck);
                if (buildCheck.Status == CheckStatuses.Pass)
                {
                    var functionalResults = await functionalTesting.RunAsync(run.Id, submission.Assignment, workspacePath, cancellationToken);
                    foreach (var functionalResult in functionalResults)
                    {
                        run.FunctionalTestResults.Add(functionalResult);
                    }
                    run.CheckResults.Add(ToFunctionalCheck(functionalResults));
                }
                else
                {
                    run.CheckResults.Add(SkippedFunctionalCheck("Functional test dilewati karena build tidak berhasil."));
                }
            }
            else
            {
                run.CheckResults.Add(new CheckResult
                {
                    CheckCode = "BUILD_SUCCESS",
                    Status = CheckStatuses.Skipped,
                    ReasonCode = "PREREQUISITE_FAILED",
                    Message = "Build dilewati karena pemeriksaan struktur proyek belum lolos.",
                    Sequence = 9
                });
                run.CheckResults.Add(SkippedFunctionalCheck("Functional test dilewati karena struktur project belum lolos."));
            }

            ApplyFinalStatus(submission, run);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Screening pipeline failed for submission {SubmissionId}", submissionId);
            run.CheckResults.Add(new CheckResult
            {
                CheckCode = "SCREENING_PIPELINE",
                Status = CheckStatuses.Error,
                ReasonCode = ReasonCodes.ScreeningPipelineError,
                Message = "Pipeline screening mengalami kesalahan internal.",
                Sequence = 99
            });
            submission.Status = SubmissionStatuses.SystemError;
            run.Status = CheckStatuses.Error;
        }
        finally
        {
            run.FinishedAtUtc = DateTime.UtcNow;
            CleanupWorkspace(workspacePath);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<CheckResult> ToBuildCheckAsync(Guid runId, CompileResult result, CancellationToken cancellationToken)
    {
        var logRelativePath = storagePaths.GetBuildLogRelativePath(runId);
        var logPath = storagePaths.ResolveStoredPath(logRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var log = $"STDOUT{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR{Environment.NewLine}{result.StandardError}";
        await File.WriteAllTextAsync(logPath, log, cancellationToken);
        var details = JsonSerializer.Serialize(new
        {
            result.ExitCode,
            result.TimedOut,
            durationMilliseconds = (long)result.Duration.TotalMilliseconds,
            logRelativePath,
            result.ErrorMessage
        });

        if (!result.Started)
        {
            return new CheckResult { CheckCode = "BUILD_SUCCESS", Status = CheckStatuses.Error, ReasonCode = ReasonCodes.RunnerUnavailable, Message = "Container runner tidak dapat dijalankan.", DetailsJson = details, Sequence = 9 };
        }
        if (result.TimedOut)
        {
            return new CheckResult { CheckCode = "BUILD_SUCCESS", Status = CheckStatuses.Fail, ReasonCode = ReasonCodes.BuildTimeout, Message = "Build melebihi batas waktu.", DetailsJson = details, Sequence = 9 };
        }
        if (result.ExitCode == 0)
        {
            return new CheckResult { CheckCode = "BUILD_SUCCESS", Status = CheckStatuses.Pass, Message = "Project berhasil di-build di container terisolasi.", DetailsJson = details, Sequence = 9 };
        }
        return new CheckResult { CheckCode = "BUILD_SUCCESS", Status = CheckStatuses.Fail, ReasonCode = ReasonCodes.BuildFailed, Message = "Project gagal di-build.", DetailsJson = details, Sequence = 9 };
    }

    private static CheckResult ToFunctionalCheck(IReadOnlyList<FunctionalTestResult> results)
    {
        if (results.Count == 0)
        {
            return new CheckResult { CheckCode = "FUNCTIONAL_TESTS", Status = CheckStatuses.Error, ReasonCode = ReasonCodes.FunctionalTestFailed, Message = "Assignment tidak memiliki functional test aktif.", Sequence = 10 };
        }
        if (results.Any(result => result.Status == CheckStatuses.Error))
        {
            return new CheckResult { CheckCode = "FUNCTIONAL_TESTS", Status = CheckStatuses.Error, ReasonCode = ReasonCodes.FunctionalTestFailed, Message = "Satu atau lebih functional test mengalami kesalahan runner.", Sequence = 10 };
        }
        if (results.Any(result => result.Status == CheckStatuses.Fail))
        {
            return new CheckResult { CheckCode = "FUNCTIONAL_TESTS", Status = CheckStatuses.Fail, ReasonCode = ReasonCodes.FunctionalTestFailed, Message = $"{results.Count(result => result.Status == CheckStatuses.Pass)} dari {results.Count} functional test lolos.", Sequence = 10 };
        }
        return new CheckResult { CheckCode = "FUNCTIONAL_TESTS", Status = CheckStatuses.Pass, Message = $"Seluruh {results.Count} functional test lolos.", Sequence = 10 };
    }

    private static CheckResult SkippedFunctionalCheck(string message) => new()
    {
        CheckCode = "FUNCTIONAL_TESTS",
        Status = CheckStatuses.Skipped,
        ReasonCode = "PREREQUISITE_FAILED",
        Message = message,
        Sequence = 10
    };

    private static void ApplyFinalStatus(Submission submission, ScreeningRun run)
    {
        if (run.CheckResults.Any(check => check.Status == CheckStatuses.Error))
        {
            submission.Status = SubmissionStatuses.SystemError;
            run.Status = CheckStatuses.Error;
        }
        else if (run.CheckResults.Any(check => check.Status == CheckStatuses.Fail))
        {
            submission.Status = SubmissionStatuses.Failed;
            run.Status = "COMPLETED";
        }
        else
        {
            submission.Status = SubmissionStatuses.Passed;
            run.Status = "COMPLETED";
        }
    }

    private static void CleanupWorkspace(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }

        var temporaryRoot = Directory.GetParent(workspacePath)?.FullName;
        if (!string.IsNullOrWhiteSpace(temporaryRoot) && Directory.Exists(temporaryRoot) && !Directory.EnumerateFileSystemEntries(temporaryRoot).Any())
        {
            Directory.Delete(temporaryRoot);
        }
    }
}
