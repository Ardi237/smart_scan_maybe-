using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;
using SmartScanner.Web.Services;
using Xunit;

namespace SmartScanner.Tests;

public sealed class SubmissionUploadAndAdministrativeScreeningTests
{
    [Fact]
    public async Task Valid_zip_is_stored_hashed_and_checked()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var archive = CreateZip("DIK_LATIHANX_BUDI SANTOSO_1301234567.zip",
            ("Program.cs", "Console.WriteLine(1);"),
            ("Latihan1.csproj", "<Project />"));

        var result = await fixture.UploadService.CreateAsync(fixture.CreateRequest(archive));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.SubmissionId);

        fixture.DbContext.ChangeTracker.Clear();
        var submission = await fixture.DbContext.Submissions
            .Include(item => item.ScreeningRuns)
                .ThenInclude(run => run.CheckResults)
            .SingleAsync(item => item.Id == result.SubmissionId);

        Assert.Equal(SubmissionStatuses.Passed, submission.Status);
        Assert.Equal(64, submission.ArchiveSha256.Length);
        Assert.True(File.Exists(fixture.StoragePaths.ResolveStoredPath(submission.ArchiveRelativePath)));
        Assert.Equal("FULL", submission.ScreeningRuns.Single().Stage);
        Assert.All(submission.ScreeningRuns.Single().CheckResults, check => Assert.Equal(CheckStatuses.Pass, check.Status));
        Assert.False(Directory.Exists(Path.Combine(fixture.StoragePaths.DataRoot, "temp")));
    }

    [Fact]
    public async Task Rejected_requests_and_archives_have_specific_reason_codes()
    {
        await using (var fixture = await TestFixture.CreateAsync())
        {
            var missingMetadata = await fixture.UploadService.CreateAsync(new SubmissionUploadRequest
            {
                AssignmentId = fixture.AssignmentId,
                StudentName = string.Empty,
                Nim = "1301234567",
                ClassName = "IF-47-01",
                AttendanceNumber = 6,
                Archive = CreateZip("anything.zip", ("Program.cs", ""))
            });
            Assert.Equal(ReasonCodes.MetadataIncomplete, missingMetadata.ReasonCode);

            var nonZip = await fixture.UploadService.CreateAsync(fixture.CreateRequest(CreateFile("anything.txt", "not a zip")));
            Assert.Equal(ReasonCodes.InvalidFileExtension, nonZip.ReasonCode);
        }

        await AssertArchiveReasonAsync(
            CreateFile("bad.zip", "this is not a zip"),
            ReasonCodes.ArchiveInvalid);

        await AssertArchiveReasonAsync(
            CreateZip("bad.zip", ("../escape.cs", "Console.WriteLine(1);"), ("Project.csproj", "<Project />")),
            ReasonCodes.ArchiveUnsafePath);

        await AssertArchiveReasonAsync(
            CreateZip("bad.zip", ("Project.csproj", "<Project />")),
            ReasonCodes.CsFileNotFound);

        await AssertArchiveReasonAsync(
            CreateZip("bad.zip", ("Program.cs", "Console.WriteLine(1);")),
            ReasonCodes.CsprojNotFound);

        await AssertArchiveReasonAsync(
            CreateZip("bad.zip", ("Program.cs", "Console.WriteLine(1);"), ("A.csproj", "<Project />"), ("B.csproj", "<Project />")),
            ReasonCodes.MultipleCsprojFound);

        await AssertArchiveReasonAsync(
            CreateZip("wrong-name.zip", ("Program.cs", "Console.WriteLine(1);"), ("Project.csproj", "<Project />")),
            ReasonCodes.FilenameMismatch);

        await using (var fixture = await TestFixture.CreateAsync(new ArchivePolicyOptions { MaxArchiveEntries = 1 }))
        {
            var result = await fixture.UploadService.CreateAsync(fixture.CreateRequest(CreateZip("bad.zip", ("Program.cs", "Console.WriteLine(1);"), ("Project.csproj", "<Project />"))));
            var reasonCodes = await fixture.GetReasonCodesAsync(result.SubmissionId!.Value);
            Assert.Contains(ReasonCodes.ArchiveFileCountLimit, reasonCodes);
        }

        await using (var fixture = await TestFixture.CreateAsync(new ArchivePolicyOptions { MaxArchiveEntryBytes = 1 }))
        {
            var result = await fixture.UploadService.CreateAsync(fixture.CreateRequest(CreateZip("bad.zip", ("Program.cs", "too large"))));
            var reasonCodes = await fixture.GetReasonCodesAsync(result.SubmissionId!.Value);
            Assert.Contains(ReasonCodes.ArchiveEntrySizeLimit, reasonCodes);
        }
    }

    [Fact]
    public async Task Compile_failure_is_persisted_as_build_failed()
    {
        await using var fixture = await TestFixture.CreateAsync(compileResult: new CompileResult(true, false, 1, TimeSpan.FromMilliseconds(25), "", "compile error", null));
        var archive = CreateZip("DIK_LATIHANX_BUDI SANTOSO_1301234567.zip",
            ("Program.cs", "Console.WriteLine(1);"),
            ("Latihan1.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />"));

        var result = await fixture.UploadService.CreateAsync(fixture.CreateRequest(archive));
        var submission = await fixture.DbContext.Submissions
            .Include(item => item.ScreeningRuns)
                .ThenInclude(run => run.CheckResults)
            .SingleAsync(item => item.Id == result.SubmissionId);
        var buildCheck = submission.ScreeningRuns.Single().CheckResults.Single(check => check.CheckCode == "BUILD_SUCCESS");

        Assert.Equal(SubmissionStatuses.Failed, submission.Status);
        Assert.Equal(CheckStatuses.Fail, buildCheck.Status);
        Assert.Equal(ReasonCodes.BuildFailed, buildCheck.ReasonCode);
        Assert.NotNull(buildCheck.DetailsJson);
        Assert.True(Directory.EnumerateFiles(Path.Combine(fixture.StoragePaths.DataRoot, "runs"), "build.log", SearchOption.AllDirectories).Any());
    }

    [Fact]
    [Trait("Category", "Container")]
    public async Task Real_container_pipeline_marks_valid_and_broken_projects_correctly()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var options = Options.Create(new ArchivePolicyOptions());
        var scanner = new AdministrativeScreeningService(fixture.StoragePaths, options, NullLogger<AdministrativeScreeningService>.Instance);
        var runner = new DockerCompileRunner(Options.Create(new RunnerOptions { BuildTimeoutSeconds = 30 }), NullLogger<DockerCompileRunner>.Instance);
        var functionalRunner = new DockerFunctionalTestRunner(Options.Create(new RunnerOptions { BuildTimeoutSeconds = 30 }), NullLogger<DockerFunctionalTestRunner>.Instance);
        var functionalTesting = new FunctionalTestingService(functionalRunner);
        var pipeline = new ScreeningPipeline(fixture.DbContext, fixture.StoragePaths, scanner, runner, functionalTesting, NullLogger<ScreeningPipeline>.Instance);
        var uploadService = new SubmissionUploadService(fixture.DbContext, fixture.StoragePaths, options, new AssistantCodeResolver(fixture.DbContext), pipeline, NullLogger<SubmissionUploadService>.Instance);

        var valid = await uploadService.CreateAsync(fixture.CreateRequest(CreateZip("DIK_LATIHANX_BUDI SANTOSO_1301234567.zip",
            ("Program.cs", "using System; var a=int.Parse(Console.ReadLine()!); var b=int.Parse(Console.ReadLine()!); var c=int.Parse(Console.ReadLine()!); Console.WriteLine(a+b+c);"),
            ("Valid.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"))));
        var broken = await uploadService.CreateAsync(fixture.CreateRequest(CreateZip("DIK_LATIHANX_BUDI SANTOSO_1301234567.zip",
            ("Program.cs", "using System; Console.WriteLine(1)"),
            ("Broken.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"))));

        fixture.DbContext.ChangeTracker.Clear();
        var submissions = await fixture.DbContext.Submissions
            .Include(item => item.ScreeningRuns)
                .ThenInclude(run => run.CheckResults)
            .Where(item => item.Id == valid.SubmissionId || item.Id == broken.SubmissionId)
            .ToListAsync();
        var validSubmission = submissions.Single(item => item.Id == valid.SubmissionId);
        var brokenSubmission = submissions.Single(item => item.Id == broken.SubmissionId);

        var validBuildCheck = validSubmission.ScreeningRuns.Single().CheckResults.Single(check => check.CheckCode == "BUILD_SUCCESS");
        Assert.Equal(SubmissionStatuses.Passed, validSubmission.Status);
        Assert.Equal(CheckStatuses.Pass, validBuildCheck.Status);
        Assert.Equal(SubmissionStatuses.Failed, brokenSubmission.Status);
        Assert.Equal(ReasonCodes.BuildFailed, brokenSubmission.ScreeningRuns.Single().CheckResults.Single(check => check.CheckCode == "BUILD_SUCCESS").ReasonCode);
    }

    private static async Task AssertArchiveReasonAsync(IFormFile archive, string expectedReasonCode)
    {
        await using var fixture = await TestFixture.CreateAsync();
        var result = await fixture.UploadService.CreateAsync(fixture.CreateRequest(archive));
        Assert.True(result.Succeeded);

        var reasonCodes = await fixture.GetReasonCodesAsync(result.SubmissionId!.Value);
        Assert.Contains(expectedReasonCode, reasonCodes);
    }

    private static IFormFile CreateZip(string fileName, params (string Path, string Contents)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, contents) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(contents);
            }
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "Archive", fileName);
    }

    private static IFormFile CreateFile(string fileName, string contents)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        return new FormFile(stream, 0, stream.Length, "Archive", fileName);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(string rootDirectory, SmartScannerDbContext dbContext, StoragePaths storagePaths, long assignmentId, ISubmissionUploadService uploadService)
        {
            RootDirectory = rootDirectory;
            DbContext = dbContext;
            StoragePaths = storagePaths;
            AssignmentId = assignmentId;
            UploadService = uploadService;
        }

        public string RootDirectory { get; }
        public SmartScannerDbContext DbContext { get; }
        public StoragePaths StoragePaths { get; }
        public long AssignmentId { get; }
        public ISubmissionUploadService UploadService { get; }

        public static async Task<TestFixture> CreateAsync(ArchivePolicyOptions? policy = null, CompileResult? compileResult = null)
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), $"smartscanner-upload-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootDirectory);
            var storagePaths = new StoragePaths(rootDirectory, Path.Combine(rootDirectory, "smartscanner.db"));
            var options = new DbContextOptionsBuilder<SmartScannerDbContext>()
                .UseSqlite($"Data Source={storagePaths.DatabasePath};Pooling=False")
                .Options;
            var dbContext = new SmartScannerDbContext(options);
            await dbContext.Database.MigrateAsync();
            await AssignmentSeedData.EnsureCreatedAsync(dbContext);
            var assignmentId = await dbContext.Assignments.Where(assignment => assignment.Code == "LATIHAN_1").Select(assignment => assignment.Id).SingleAsync();

            policy ??= new ArchivePolicyOptions();
            var scanner = new AdministrativeScreeningService(storagePaths, Options.Create(policy), NullLogger<AdministrativeScreeningService>.Instance);
            var runner = new FakeCompileRunner(compileResult ?? new CompileResult(true, false, 0, TimeSpan.FromMilliseconds(25), "Build succeeded", "", null));
            var functionalTesting = new FakeFunctionalTestingService();
            var pipeline = new ScreeningPipeline(dbContext, storagePaths, scanner, runner, functionalTesting, NullLogger<ScreeningPipeline>.Instance);
            var uploadService = new SubmissionUploadService(dbContext, storagePaths, Options.Create(policy), new AssistantCodeResolver(dbContext), pipeline, NullLogger<SubmissionUploadService>.Instance);
            return new TestFixture(rootDirectory, dbContext, storagePaths, assignmentId, uploadService);
        }

        public SubmissionUploadRequest CreateRequest(IFormFile archive) => new()
        {
            AssignmentId = AssignmentId,
            StudentName = "Budi Santoso",
            Nim = "1301234567",
            ClassName = "IF-47-01",
            AttendanceNumber = 6,
            Archive = archive
        };

        public async Task<IReadOnlyList<string>> GetReasonCodesAsync(Guid submissionId) => await DbContext.CheckResults
            .Where(check => check.ScreeningRun.SubmissionId == submissionId && check.ReasonCode != null)
            .Select(check => check.ReasonCode!)
            .ToListAsync();

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeCompileRunner(CompileResult result) : ICompileRunner
    {
        public Task<CompileResult> BuildAsync(Guid runId, string workspacePath, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeFunctionalTestingService : IFunctionalTestingService
    {
        public Task<IReadOnlyList<FunctionalTestResult>> RunAsync(Guid runId, Assignment assignment, string workspacePath, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<FunctionalTestResult> results = assignment.TestCases.Where(test => test.IsActive).Select(test => new FunctionalTestResult
            {
                TestCaseId = test.Id,
                Status = CheckStatuses.Pass,
                StandardInput = test.StandardInput,
                StandardOutput = test.ExpectedValuesJson,
                StandardError = string.Empty,
                ExpectedValuesJson = test.ExpectedValuesJson,
                ActualValuesJson = test.ExpectedValuesJson,
                ExitCode = 0,
                Sequence = test.Sequence
            }).ToList();
            return Task.FromResult(results);
        }
    }
}
