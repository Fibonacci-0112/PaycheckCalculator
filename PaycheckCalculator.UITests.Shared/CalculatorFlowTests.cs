namespace PaycheckCalculator.UITests;

/// <summary>
/// Drives a real paycheck calculation through the UI on a real device.
/// </summary>
/// <remarks>
/// <para>
/// The unit suite already covers the tax math exhaustively and with far better precision,
/// so this deliberately does not re-assert withholding amounts. What it covers is the part
/// unit tests structurally cannot: that on an actual device the app can read its tax tables
/// out of the app package, run the pipeline, and render the result.
/// </para>
/// <para>
/// That makes it the regression test for the packaging hazard called out in CLAUDE.md —
/// renaming a file under <c>PaycheckCalculator.Core/Data/</c> without updating the
/// <c>MauiAsset</c> entries builds clean and fails only at run time, on device.
/// </para>
/// </remarks>
[TestFixture]
public class CalculatorFlowTests : BaseTest
{
    [SetUp]
    public void GoToInputs() => GoToTab("Inputs");

    [Test]
    public void HourlyPaycheckProducesANetPayResult()
    {
        WaitForElement("InputsTab_PayHours").Click();

        // $40/hr x 40 regular hours + 5 hours of overtime.
        SetText("Input_HourlyRate", "40");
        SetText("Input_RegularHours", "40");
        SetText("Input_OvertimeHours", "5");

        FindElement("Button_Calculate_PayHours").Click();

        GoToTab("Results");

        var netPayText = WaitForElement("Results_NetPay").Text;
        var netPay = ParseCurrency(netPayText);

        // Gross is $1,900 before withholding; the exact net depends on the W-4 and state
        // defaults, so assert only that a plausible figure was produced end to end.
        Assert.Multiple(() =>
        {
            Assert.That(netPay, Is.GreaterThan(0m),
                $"Net pay rendered as '{netPayText}', so the calculation did not produce a result.");
            Assert.That(netPay, Is.LessThan(1900m),
                $"Net pay '{netPayText}' is not below gross pay — withholding was not applied.");
        });
    }

    [Test]
    public void AnnualProjectionIsShownOnTheAnnualTab()
    {
        WaitForElement("InputsTab_PayHours").Click();
        SetText("Input_HourlyRate", "50");
        SetText("Input_RegularHours", "40");
        SetText("Input_OvertimeHours", "0");
        FindElement("Button_Calculate_PayHours").Click();

        GoToTab("Results");

        var perPeriodNetPay = ParseCurrency(WaitForElement("Results_NetPay").Text);
        var annualNetPay = ParseCurrency(WaitForElement("Results_AnnualNetPay").Text);

        // Whatever the default frequency, a year holds many pay periods, so the annualized
        // figure must be a large multiple of one paycheck. Deliberately a loose bound: the
        // point is to catch a projection that renders zero or mirrors the per-period value,
        // not to re-assert the arithmetic the unit suite already pins down exactly.
        Assert.That(annualNetPay, Is.GreaterThan(perPeriodNetPay * 10),
            "The annual projection is not consistent with the per-period net pay.");
    }
}
