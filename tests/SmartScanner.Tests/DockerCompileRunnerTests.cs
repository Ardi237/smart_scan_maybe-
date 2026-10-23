using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Services;
using Xunit;

namespace SmartScanner.Tests;

public sealed class DockerCompileRunnerTests
{
    [Fact]
    [Trait("Category", "Container")]
    public async Task Valid_project_passes_and_broken_project_returns_nonzero_exit_code()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runner = new DockerCompileRunner(
            Options.Create(new RunnerOptions { BuildTimeoutSeconds = 30 }),
            NullLogger<DockerCompileRunner>.Instance);

        var valid = await runner.BuildAsync(Guid.NewGuid(), Path.Combine(repositoryRoot, "samples", "valid"));
        var broken = await runner.BuildAsync(Guid.NewGuid(), Path.Combine(repositoryRoot, "samples", "build-fail"));

        Assert.True(valid.Started, valid.ErrorMessage);
        Assert.False(valid.TimedOut);
        Assert.Equal(0, valid.ExitCode);
        Assert.True(broken.Started, broken.ErrorMessage);
        Assert.False(broken.TimedOut);
        Assert.NotEqual(0, broken.ExitCode);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartScanner.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("SmartScanner.sln could not be found from the test output directory.");
    }
}
