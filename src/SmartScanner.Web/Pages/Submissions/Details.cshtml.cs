using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;
using SmartScanner.Web.Services;

namespace SmartScanner.Web.Pages.Submissions;

public sealed class DetailsModel(SmartScannerDbContext dbContext, IScreeningPipeline screeningPipeline) : PageModel
{
    public Submission Submission { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var submission = await dbContext.Submissions
            .AsNoTracking()
            .Include(item => item.Assignment)
            .Include(item => item.ScreeningRuns)
                .ThenInclude(run => run.CheckResults)
            .Include(item => item.ScreeningRuns)
                .ThenInclude(run => run.FunctionalTestResults)
                    .ThenInclude(result => result.TestCase)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        Submission = submission;
        return Page();
    }

    public async Task<IActionResult> OnPostRecheckAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await dbContext.Submissions.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
        await screeningPipeline.RunAsync(id, cancellationToken);
        return RedirectToPage(new { id });
    }
}
