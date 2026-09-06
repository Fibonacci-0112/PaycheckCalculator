using System.IO;
using PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Test-only access to the real employee-paid payroll assessment table, loaded
/// once from the <c>state_payroll_assessments_2026.json</c> copied into the test
/// bin output. Calculator tests use the shipping rates rather than fixtures, so
/// an expected value in a test is the same number the app produces.
/// </summary>
public static class TestAssessments
{
    public static StatePayrollAssessments Table { get; } = StatePayrollAssessments.Load(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "state_payroll_assessments_2026.json")));
}
