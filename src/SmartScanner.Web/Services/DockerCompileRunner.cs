using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;

namespace SmartScanner.Web.Services;

public sealed class DockerCompileRunner(
    IOptions<RunnerOptions> options,
    ILogger<DockerCompileRunner> logger) : ICompileRunner
{
    public async Task<CompileResult> BuildAsync(Guid runId, string workspacePath, CancellationToken cancellationToken = default)
    {
        var runner = options.Value;
        var containerName = $"smartscanner-build-{runId:N}";
        var stopwatch = Stopwatch.StartNew();
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            AddDockerArguments(startInfo, containerName, workspacePath, runner);
            process = Process.Start(startInfo);
            if (process is null)
            {
                return new CompileResult(false, false, null, stopwatch.Elapsed, string.Empty, string.Empty, "Docker runner tidak dapat dimulai.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var exitedTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, runner.BuildTimeoutSeconds)), cancellationToken);
            var completedTask = await Task.WhenAny(exitedTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                await RemoveContainerAsync(containerName, cancellationToken);
                await Task.WhenAll(stdoutTask, stderrTask);
                return new CompileResult(true, true, null, stopwatch.Elapsed, await stdoutTask, await stderrTask, null);
            }

            await exitedTask;
            await Task.WhenAll(stdoutTask, stderrTask);
            return new CompileResult(true, false, process.ExitCode, stopwatch.Elapsed, await stdoutTask, await stderrTask, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RemoveContainerAsync(containerName, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Docker build runner could not execute for {RunId}", runId);
            return new CompileResult(false, false, null, stopwatch.Elapsed, string.Empty, string.Empty, exception.Message);
        }
        finally
        {
            if (process is not null)
            {
                process.Dispose();
            }

            await RemoveContainerAsync(containerName, CancellationToken.None);
        }
    }

    private static void AddDockerArguments(ProcessStartInfo startInfo, string containerName, string workspacePath, RunnerOptions runner)
    {
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--name");
        startInfo.ArgumentList.Add(containerName);
        startInfo.ArgumentList.Add("--rm");
        startInfo.ArgumentList.Add("--network");
        startInfo.ArgumentList.Add("none");
        startInfo.ArgumentList.Add("--read-only");
        startInfo.ArgumentList.Add("--cap-drop");
        startInfo.ArgumentList.Add("ALL");
        startInfo.ArgumentList.Add("--security-opt");
        startInfo.ArgumentList.Add("no-new-privileges");
        startInfo.ArgumentList.Add("--pids-limit");
        startInfo.ArgumentList.Add(runner.ProcessLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--memory");
        startInfo.ArgumentList.Add(runner.MemoryLimit);
        startInfo.ArgumentList.Add("--cpus");
        startInfo.ArgumentList.Add(runner.CpuLimit);
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add("10001:10001");
        startInfo.ArgumentList.Add("--tmpfs");
        startInfo.ArgumentList.Add("/tmp:rw,exec,size=64m");
        startInfo.ArgumentList.Add("--tmpfs");
        startInfo.ArgumentList.Add("/workspace:rw,exec,size=128m");
        startInfo.ArgumentList.Add("--tmpfs");
        startInfo.ArgumentList.Add("/home/runner/.nuget:rw,size=64m");
        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add($"type=bind,src={Path.GetFullPath(workspacePath)},dst=/input,readonly");
        startInfo.ArgumentList.Add("--entrypoint");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add(runner.ImageName);
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add("set -eu; cp -R /input/. /workspace/; find /workspace -type d \\( -name bin -o -name obj \\) -prune -exec rm -rf {} +; project_file=$(find /workspace -type f -name '*.csproj' -print -quit); test -n \"$project_file\"; cd \"$(dirname \"$project_file\")\"; dotnet restore --disable-parallel --ignore-failed-sources --nologo; dotnet build --no-restore --nologo");
    }

    private static async Task RemoveContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            using var cleanup = new Process
            {
                StartInfo = new ProcessStartInfo("docker")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            cleanup.StartInfo.ArgumentList.Add("rm");
            cleanup.StartInfo.ArgumentList.Add("-f");
            cleanup.StartInfo.ArgumentList.Add(containerName);
            if (cleanup.Start())
            {
                await cleanup.WaitForExitAsync(cancellationToken);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A completed --rm container no longer exists; cleanup is intentionally best effort.
        }
    }
}
