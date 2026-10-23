using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using SmartScanner.Web.Configuration;
using SmartScanner.Web.Data;
using SmartScanner.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddDebug();

builder.Services.AddRazorPages();
builder.Services.Configure<ArchivePolicyOptions>(builder.Configuration.GetSection(ArchivePolicyOptions.SectionName));
builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection(RunnerOptions.SectionName));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration.GetValue<long>($"{ArchivePolicyOptions.SectionName}:MaxUploadBytes", 50 * 1024 * 1024);
});

var configuredDataRoot = builder.Configuration.GetValue<string>($"{StorageOptions.SectionName}:DataRoot") ?? "data";
var dataRoot = Path.IsPathRooted(configuredDataRoot)
    ? configuredDataRoot
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, configuredDataRoot));

Directory.CreateDirectory(dataRoot);
var databasePath = Path.Combine(dataRoot, "smartscanner.db");

builder.Services.Configure<StorageOptions>(options => options.DataRoot = dataRoot);
builder.Services.AddSingleton(new StoragePaths(dataRoot, databasePath));
builder.Services.AddDbContext<SmartScannerDbContext>(options =>
    options.UseSqlite(
        $"Data Source={databasePath}",
        sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddScoped<IAdministrativeScreeningService, AdministrativeScreeningService>();
builder.Services.AddScoped<IAssistantCodeResolver, AssistantCodeResolver>();
builder.Services.AddScoped<ICompileRunner, DockerCompileRunner>();
builder.Services.AddScoped<IFunctionalTestRunner, DockerFunctionalTestRunner>();
builder.Services.AddScoped<IFunctionalTestingService, FunctionalTestingService>();
builder.Services.AddScoped<IScreeningPipeline, ScreeningPipeline>();
builder.Services.AddScoped<ISubmissionUploadService, SubmissionUploadService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartScannerDbContext>();
    await dbContext.Database.MigrateAsync();
    await AssignmentSeedData.EnsureCreatedAsync(dbContext);
    app.Logger.LogInformation("SmartScanner database is ready at {DatabasePath}", databasePath);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

public partial class Program;
