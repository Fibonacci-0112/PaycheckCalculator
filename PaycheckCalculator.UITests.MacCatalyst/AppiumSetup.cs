using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Mac;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Creates the Mac Catalyst (Mac2) Appium driver for this assembly's tests.
/// </summary>
/// <remarks>
/// macOS only. The Mac2 driver automates through Apple's accessibility APIs, so the run
/// needs a real (or virtualized) macOS desktop session — a headless SSH shell has no window
/// server to attach to.
/// </remarks>
[SetUpFixture]
public class AppiumSetup
{
    private static MacDriver? _driver;

    public static AppiumDriver? App => _driver;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = "Mac2",
            PlatformName = "Mac",
        };

        // BundleId is mandatory for Mac2. Without it the driver happily attaches to Finder
        // and every test then fails looking for elements that were never going to be there.
        options.AddAdditionalAppiumOption("bundleId", TestConfig.AppId);

        if (TestConfig.AppPath is { } appBundle)
        {
            // Mac2 launches an .app bundle by path when it isn't in /Applications.
            options.AddAdditionalAppiumOption("appPath", appBundle);
        }

        options.AddAdditionalAppiumOption("noReset", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("showServerLogs", true);

        _driver = new MacDriver(TestConfig.ServerUri, options, TimeSpan.FromMinutes(5));
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        _driver?.Quit();
        _driver?.Dispose();
        _driver = null;
        AppiumServerHelper.DisposeAppiumLocalServer();
    }
}
