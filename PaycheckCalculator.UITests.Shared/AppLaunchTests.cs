namespace PaycheckCalculator.UITests;

/// <summary>
/// The cheapest test that proves the app <em>runs</em> rather than merely compiles.
/// </summary>
/// <remarks>
/// A MAUI app can build perfectly and still die on launch — a service missing from
/// <c>MauiProgram</c>'s container, a XAML resource key that only resolves at run time, or a
/// tax JSON file that was renamed without updating the <c>MauiAsset</c> entries in the
/// .csproj. None of that is visible to the compiler. If this test passes, the app started,
/// built its DI container, loaded the Shell, and rendered its first page.
/// </remarks>
[TestFixture]
public class AppLaunchTests : BaseTest
{
    [Test]
    public void AppLaunchesAndShowsTheInputsPage()
    {
        // The Pay & Hours sub-tab is the default selection on the landing page.
        var payHoursTab = WaitForElement("InputsTab_PayHours", TimeSpan.FromSeconds(60));

        Assert.That(payHoursTab.Displayed, Is.True, "The Inputs page did not render after launch.");
    }

    [Test]
    public void AllShellTabsAreReachable()
    {
        // Walking the whole TabBar forces every page's constructor, BindingContext, and
        // initial data load to run — several of which touch the tax-data reader and the
        // on-device paycheck store.
        foreach (var tab in new[] { "Results", "Paychecks", "Budget", "Account", "Inputs" })
        {
            Assert.DoesNotThrow(() => GoToTab(tab), $"Navigating to the '{tab}' tab failed.");
        }
    }
}
