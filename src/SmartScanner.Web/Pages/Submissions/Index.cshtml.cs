using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Pages.Submissions;

public sealed class IndexModel(SmartScannerDbContext dbContext) : PageModel
{
    public IReadOnlyList<Submission> Submissions { get; private set; } = [];
    public IReadOnlyList<Assignment> Assignments { get; private set; } = [];
    public IReadOnlyList<string> Classes { get; private set; } = [];
    public IReadOnlyList<string> ReasonCodes { get; private set; } = [];
    public string? Status { get; private set; }
    public string? ClassName { get; private set; }
    public long? AssignmentId { get; private set; }
    public string? ReasonCode { get; private set; }

    public async Task OnGetAsync(string? status, string? className, long? assignmentId, string? reasonCode)
    {
        Status = status;
        ClassName = className;
        AssignmentId = assignmentId;
        ReasonCode = reasonCode;

        var query = dbContext.Submissions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(className)) query = query.Where(item => item.ClassName == className);
        if (assignmentId.HasValue) query = query.Where(item => item.AssignmentId == assignmentId);
        if (!string.IsNullOrWhiteSpace(reasonCode))
            query = query.Where(item => item.ScreeningRuns.Any(run => run.CheckResults.Any(check => check.ReasonCode == reasonCode) || run.FunctionalTestResults.Any(test => test.ReasonCode == reasonCode)));

        Submissions = await query
            .Include(item => item.Assignment)
            .Include(item => item.ScreeningRuns).ThenInclude(run => run.CheckResults)
            .Include(item => item.ScreeningRuns).ThenInclude(run => run.FunctionalTestResults)
            .OrderByDescending(item => item.UploadedAtUtc)
            .ToListAsync();
        Assignments = await dbContext.Assignments.AsNoTracking().OrderBy(item => item.Code).ToListAsync();
        Classes = await dbContext.Submissions.AsNoTracking().Select(item => item.ClassName).Distinct().OrderBy(item => item).ToListAsync();
        var checkReasons = dbContext.CheckResults.Where(item => item.ReasonCode != null).Select(item => item.ReasonCode!);
        var testReasons = dbContext.FunctionalTestResults.Where(item => item.ReasonCode != null).Select(item => item.ReasonCode!);
        ReasonCodes = await checkReasons.Union(testReasons).OrderBy(item => item).ToListAsync();
    }
}
