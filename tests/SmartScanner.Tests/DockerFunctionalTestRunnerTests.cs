using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Models;
using SmartScanner.Web.Services;
using Xunit;

namespace SmartScanner.Tests;

public sealed class DockerFunctionalTestRunnerTests
{
    [Fact]
    [Trait("Category", "Container")]
    public async Task Runner_redirects_stdin_captures_stdout_and_stops_infinite_loop()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runner = new DockerFunctionalTestRunner(
            Options.Create(new RunnerOptions { BuildTimeoutSeconds = 30 }),
            NullLogger<DockerFunctionalTestRunner>.Instance);
        var validCase = new TestCase { Id = 9001, StandardInput = "7\n8\n" };
        var timeoutCase = new TestCase { Id = 9002, StandardInput = string.Empty };

        var valid = await runner.RunAsync(Guid.NewGuid(), Path.Combine(repositoryRoot, "samples", "valid"), validCase, 2);
        var infinite = await runner.RunAsync(Guid.NewGuid(), Path.Combine(repositoryRoot, "samples", "infinite-loop"), timeoutCase, 1);

        Assert.True(valid.Started, valid.ErrorMessage);
        Assert.False(valid.TimedOut);
        Assert.Equal(0, valid.ExitCode);
        Assert.Contains("15", valid.StandardOutput);
        Assert.True(infinite.Started, infinite.ErrorMessage);
        Assert.True(infinite.TimedOut);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartScanner.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("SmartScanner.sln could not be found from the test output directory.");
    }
}
