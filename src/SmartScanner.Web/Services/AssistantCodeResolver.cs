using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;

namespace SmartScanner.Web.Services;

public sealed class AssistantCodeResolver(SmartScannerDbContext dbContext) : IAssistantCodeResolver
{
    public Task<string?> ResolveAsync(int attendanceNumber, CancellationToken cancellationToken = default) =>
        dbContext.AssistantAssignmentRules
            .AsNoTracking()
            .Where(rule => rule.IsActive && attendanceNumber >= rule.MinimumAttendanceNumber && attendanceNumber <= rule.MaximumAttendanceNumber)
            .OrderBy(rule => rule.MinimumAttendanceNumber)
            .Select(rule => rule.AssistantCode)
            .FirstOrDefaultAsync(cancellationToken);
}
