using Microsoft.EntityFrameworkCore;
using SmartScanner.Web.Data;
using Xunit;

namespace SmartScanner.Tests;

public sealed class InitialMigrationTests
{
    [Fact]
    public async Task Migration_and_seed_data_create_the_assignment_catalog()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartscanner-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SmartScannerDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        try
        {
            await using (var dbContext = new SmartScannerDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                await AssignmentSeedData.EnsureCreatedAsync(dbContext);

                var assignments = await dbContext.Assignments
                    .Include(assignment => assignment.TestCases)
                .OrderBy(assignment => assignment.Id)
                    .ToListAsync();

                Assert.Collection(
                    assignments,
                    latihan =>
                    {
                        Assert.Equal("LATIHAN_1", latihan.Code);
                        Assert.Equal(4, latihan.TestCases.Count);
                    },
                    jurnal =>
                    {
                        Assert.Equal("JURNAL_1", jurnal.Code);
                        Assert.Equal(4, jurnal.TestCases.Count);
                    });

                var rules = await dbContext.AssistantAssignmentRules.OrderBy(rule => rule.MinimumAttendanceNumber).ToListAsync();
                Assert.Collection(rules,
                    rule => { Assert.Equal((1, 15, "DIK"), (rule.MinimumAttendanceNumber, rule.MaximumAttendanceNumber, rule.AssistantCode)); },
                    rule => { Assert.Equal((16, 30, "ARD"), (rule.MinimumAttendanceNumber, rule.MaximumAttendanceNumber, rule.AssistantCode)); },
                    rule => { Assert.Equal((31, 45, "ELL"), (rule.MinimumAttendanceNumber, rule.MaximumAttendanceNumber, rule.AssistantCode)); });
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
