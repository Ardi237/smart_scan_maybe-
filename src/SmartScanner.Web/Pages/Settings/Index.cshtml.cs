using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;

namespace SmartScanner.Web.Pages.Settings;

public sealed class IndexModel(SmartScannerDbContext dbContext) : PageModel
{
    [BindProperty]
    public List<RuleInput> Rules { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var ordered = Rules.OrderBy(rule => rule.MinimumAttendanceNumber).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var rule = ordered[index];
            if (rule.MinimumAttendanceNumber > rule.MaximumAttendanceNumber)
                ModelState.AddModelError(string.Empty, $"Rentang {rule.AssistantCode}: batas awal tidak boleh lebih besar dari batas akhir.");
            if (index > 0 && rule.MinimumAttendanceNumber <= ordered[index - 1].MaximumAttendanceNumber)
                ModelState.AddModelError(string.Empty, "Rentang nomor absen tidak boleh tumpang tindih.");
        }

        if (!ModelState.IsValid) return Page();

        var ids = Rules.Select(rule => rule.Id).ToList();
        var entities = await dbContext.AssistantAssignmentRules.Where(rule => ids.Contains(rule.Id)).ToListAsync(cancellationToken);
        if (entities.Count != Rules.Count) return BadRequest();

        foreach (var input in Rules)
        {
            var entity = entities.Single(rule => rule.Id == input.Id);
            entity.MinimumAttendanceNumber = input.MinimumAttendanceNumber;
            entity.MaximumAttendanceNumber = input.MaximumAttendanceNumber;
            entity.AssistantCode = input.AssistantCode.Trim().ToUpperInvariant();
            entity.IsActive = input.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        SuccessMessage = "General Settings berhasil disimpan.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Rules = await dbContext.AssistantAssignmentRules.AsNoTracking()
            .OrderBy(rule => rule.MinimumAttendanceNumber)
            .Select(rule => new RuleInput { Id = rule.Id, MinimumAttendanceNumber = rule.MinimumAttendanceNumber, MaximumAttendanceNumber = rule.MaximumAttendanceNumber, AssistantCode = rule.AssistantCode, IsActive = rule.IsActive })
            .ToListAsync();
    }

    public sealed class RuleInput
    {
        public long Id { get; set; }
        [Range(1, 999)] public int MinimumAttendanceNumber { get; set; }
        [Range(1, 999)] public int MaximumAttendanceNumber { get; set; }
        [Required, StringLength(32)] public string AssistantCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
