using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;
using SmartScanner.Web.Services;

namespace SmartScanner.Web.Pages.Submissions;

public sealed class CreateModel(
    SmartScannerDbContext dbContext,
    ISubmissionUploadService submissionUploadService) : PageModel
{
    [BindProperty]
    public UploadInput Input { get; set; } = new();

    public IReadOnlyList<AssignmentOption> Assignments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAssignmentsAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadAssignmentsAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await submissionUploadService.CreateAsync(new SubmissionUploadRequest
        {
            AssignmentId = Input.AssignmentId,
            StudentName = Input.StudentName,
            Nim = Input.Nim,
            ClassName = Input.ClassName,
            AttendanceNumber = Input.AttendanceNumber,
            Archive = Input.Archive
        }, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, $"[{result.ReasonCode}] {result.Message}");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.SubmissionId });
    }

    private async Task LoadAssignmentsAsync()
    {
        Assignments = await dbContext.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.IsActive)
            .OrderBy(assignment => assignment.Code)
            .Select(assignment => new AssignmentOption(assignment.Id, assignment.Code, assignment.Name))
            .ToListAsync();
    }

    public sealed class UploadInput
    {
        [Range(1, long.MaxValue, ErrorMessage = "Assignment wajib dipilih.")]
        public long AssignmentId { get; set; }

        [Required, StringLength(200)]
        public string StudentName { get; set; } = string.Empty;

        [Required, RegularExpression("^[0-9]{8,20}$", ErrorMessage = "NIM harus berupa 8 sampai 20 digit angka.")]
        public string Nim { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ClassName { get; set; } = string.Empty;

        [Range(1, 999, ErrorMessage = "Nomor absen harus lebih dari 0.")]
        public int AttendanceNumber { get; set; }

        [Required(ErrorMessage = "File ZIP wajib dipilih.")]
        public IFormFile? Archive { get; set; }
    }

    public sealed record AssignmentOption(long Id, string Code, string Name);
}
