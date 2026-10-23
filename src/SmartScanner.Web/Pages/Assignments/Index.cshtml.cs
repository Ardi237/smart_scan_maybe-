using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Pages.Assignments;

public sealed class IndexModel(SmartScannerDbContext dbContext) : PageModel
{
    public IReadOnlyList<Assignment> Assignments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Assignments = await dbContext.Assignments
            .AsNoTracking()
            .Include(assignment => assignment.TestCases)
            .OrderBy(assignment => assignment.Code)
            .ToListAsync();
    }
}
