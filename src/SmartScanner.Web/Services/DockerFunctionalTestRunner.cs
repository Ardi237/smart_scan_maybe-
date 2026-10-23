using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public sealed class DockerFunctionalTestRunner(
    IOptions<RunnerOptions> options,
    ILogger<DockerFunctionalTestRunner> logger) : IFunctionalTestRunner
{
    public async Task<FunctionalExecutionResult> RunAsync(Guid runId, string workspacePath, TestCase testCase, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var runner = options.Value;
        var containerName = $"smartscanner-test-{runId:N}-{testCase.Id}";
        var stopwatch = Stopwatch.StartNew();
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            AddArguments(startInfo, containerName, workspacePath, runner, timeoutSeconds);
            process = Process.Start(startInfo);
            if (process is null)
            {
                return new FunctionalExecutionResult(false, false, null, stopwatch.Elapsed, string.Empty, string.Empty, "Docker runner tidak dapat dimulai.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteAsync(testCase.StandardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();

            var exitedTask = process.WaitForExitAsync(cancellationToken);
            var hostLimit = Math.Max(5, runner.BuildTimeoutSeconds) + Math.Max(1, timeoutSeconds) + 5;
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(hostLimit), cancellationToken);
            if (await Task.WhenAny(exitedTask, timeoutTask) == timeoutTask)
            {
                await RemoveContainerAsync(containerName, CancellationToken.None);
                await Task.WhenAll(stdoutTask, stderrTask);
                return new FunctionalExecutionResult(true, true, null, stopwatch.Elapsed, await stdoutTask, await stderrTask, null);
            }

            await exitedTask;
            await Task.WhenAll(stdoutTask, stderrTask);
            return new FunctionalExecutionResult(true, process.ExitCode is 124 or 137, process.ExitCode, stopwatch.Elapsed, await stdoutTask, await stderrTask, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RemoveContainerAsync(containerName, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Functional runner failed for run {RunId}, test case {TestCaseId}", runId, testCase.Id);
            return new FunctionalExecutionResult(false, false, null, stopwatch.Elapsed, string.Empty, string.Empty, exception.Message);
        }
        finally
        {
            process?.Dispose();
            await RemoveContainerAsync(containerName, CancellationToken.None);
        }
    }

    private static void AddArguments(ProcessStartInfo startInfo, string containerName, string workspacePath, RunnerOptions runner, int timeoutSeconds)
    {
        string[] arguments =
        [
            "run", "--name", containerName, "--rm", "-i",
            "--network", "none", "--read-only", "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges", "--pids-limit", runner.ProcessLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--memory", runner.MemoryLimit, "--cpus", runner.CpuLimit, "--user", "10001:10001",
            "--tmpfs", "/tmp:rw,exec,size=64m", "--tmpfs", "/workspace:rw,exec,size=128m",
            "--tmpfs", "/home/runner/.nuget:rw,size=64m",
            "--mount", $"type=bind,src={Path.GetFullPath(workspacePath)},dst=/input,readonly",
            "--entrypoint", "sh", runner.ImageName, "-lc",
            $"set -eu; cp -R /input/. /workspace/; find /workspace -type d \\( -name bin -o -name obj \\) -prune -exec rm -rf {{}} +; project_file=$(find /workspace -type f -name '*.csproj' -print -quit); test -n \"$project_file\"; cd \"$(dirname \"$project_file\")\"; dotnet restore --disable-parallel --ignore-failed-sources --nologo >/tmp/build.log 2>&1; dotnet build --no-restore --nologo >>/tmp/build.log 2>&1; timeout --kill-after=1s {Math.Max(1, timeoutSeconds)}s dotnet run --no-build --no-restore"
        ];
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static async Task RemoveContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            using var cleanup = new Process { StartInfo = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
            cleanup.StartInfo.ArgumentList.Add("rm");
            cleanup.StartInfo.ArgumentList.Add("-f");
            cleanup.StartInfo.ArgumentList.Add(containerName);
            if (cleanup.Start()) await cleanup.WaitForExitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // --rm normally removes the container before this best-effort cleanup.
        }
    }
}
