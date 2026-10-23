using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartScanner.Web.Models;

namespace SmartScanner.Web.Services;

public sealed partial class FunctionalTestingService(IFunctionalTestRunner runner) : IFunctionalTestingService
{
    public async Task<IReadOnlyList<FunctionalTestResult>> RunAsync(Guid runId, Assignment assignment, string workspacePath, CancellationToken cancellationToken = default)
    {
        var results = new List<FunctionalTestResult>();
        foreach (var testCase in assignment.TestCases.Where(item => item.IsActive).OrderBy(item => item.Sequence))
        {
            var execution = await runner.RunAsync(runId, workspacePath, testCase, assignment.RunTimeoutSeconds, cancellationToken);
            results.Add(Evaluate(testCase, execution, assignment.NumericTolerance));
        }
        return results;
    }

    public static FunctionalTestResult Evaluate(TestCase testCase, FunctionalExecutionResult execution, double tolerance)
    {
        var result = new FunctionalTestResult
        {
            TestCaseId = testCase.Id,
            StandardInput = testCase.StandardInput,
            StandardOutput = Limit(execution.StandardOutput),
            StandardError = Limit(execution.StandardError),
            ExpectedValuesJson = testCase.ExpectedValuesJson,
            ExitCode = execution.ExitCode,
            TimedOut = execution.TimedOut,
            DurationMilliseconds = (long)execution.Duration.TotalMilliseconds,
            Sequence = testCase.Sequence
        };

        if (!execution.Started)
        {
            result.Status = CheckStatuses.Error;
            result.ReasonCode = ReasonCodes.RunnerUnavailable;
            return result;
        }
        if (execution.TimedOut)
        {
            result.Status = CheckStatuses.Fail;
            result.ReasonCode = ReasonCodes.FunctionalTestTimeout;
            return result;
        }
        if (execution.ExitCode != 0)
        {
            result.Status = CheckStatuses.Fail;
            result.ReasonCode = ReasonCodes.ProgramCrashed;
            return result;
        }

        try
        {
            var expected = JsonSerializer.Deserialize<double[]>(testCase.ExpectedValuesJson) ?? [];
            var allNumbers = NumberTokenRegex().Matches(execution.StandardOutput)
                .Select(match => double.Parse(match.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();
            var actual = allNumbers.Length >= expected.Length ? allNumbers[^expected.Length..] : allNumbers;
            result.ActualValuesJson = JsonSerializer.Serialize(actual);
            var matches = actual.Length == expected.Length && actual.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) <= tolerance);
            result.Status = matches ? CheckStatuses.Pass : CheckStatuses.Fail;
            result.ReasonCode = matches ? null : ReasonCodes.OutputMismatch;
        }
        catch (Exception)
        {
            result.Status = CheckStatuses.Error;
            result.ReasonCode = ReasonCodes.FunctionalTestFailed;
        }

        return result;
    }

    private static string Limit(string value) => value.Length <= 65_536 ? value : value[..65_536];

    [GeneratedRegex(@"(?<![\p{L}\d])[-+]?(?:\d+(?:[.,]\d+)?|[.,]\d+)(?:[eE][-+]?\d+)?(?![\p{L}\d])", RegexOptions.CultureInvariant)]
    private static partial Regex NumberTokenRegex();
}
