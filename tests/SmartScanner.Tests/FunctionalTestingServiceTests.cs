using SmartScanner.Web.Models;
using SmartScanner.Web.Services;
using Xunit;

namespace SmartScanner.Tests;

public sealed class FunctionalTestingServiceTests
{
    [Fact]
    public void Last_numbers_ignores_unit_suffix_and_accepts_decimal_tolerance()
    {
        var testCase = new TestCase { Id = 1, Sequence = 1, ExpectedValuesJson = "[23,30]", StandardInput = "7.5\n4\n" };
        var execution = new FunctionalExecutionResult(true, false, 0, TimeSpan.FromMilliseconds(10), "Keliling: 23,00001 m\nLuas: 30 m2", "", null);

        var result = FunctionalTestingService.Evaluate(testCase, execution, 0.0001d);

        Assert.Equal(CheckStatuses.Pass, result.Status);
        Assert.Null(result.ReasonCode);
        Assert.Equal("[23.00001,30]", result.ActualValuesJson);
    }

    [Fact]
    public void Hardcoded_pdf_answer_fails_alternative_case()
    {
        var alternative = new TestCase { Id = 2, Sequence = 2, ExpectedValuesJson = "[60]", StandardInput = "10\n20\n30\n" };
        var hardcoded = new FunctionalExecutionResult(true, false, 0, TimeSpan.FromMilliseconds(10), "Total buah: 90", "", null);

        var result = FunctionalTestingService.Evaluate(alternative, hardcoded, 0.0001d);

        Assert.Equal(CheckStatuses.Fail, result.Status);
        Assert.Equal(ReasonCodes.OutputMismatch, result.ReasonCode);
    }
}
