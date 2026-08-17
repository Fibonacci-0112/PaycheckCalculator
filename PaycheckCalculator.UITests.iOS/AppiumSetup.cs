using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Creates the iOS (XCUITest) Appium driver for this assembly's tests.
/// </summary>
/// <remarks>
/// <para>
/// macOS only — the XCUITest driver builds and deploys WebDriverAgent with Xcode, which
/// Apple does not ship for any other host OS. There is no Linux or Windows workaround.
/// </para>
/// <para>
/// CI runs this against the Simulator, where an unsigned <c>iossimulator-*</c> build
/// installs without a provisioning profile. Testing on a physical device additionally
/// requires a signing identity.
/// </para>
/// </remarks>
[SetUpFixture]
public class AppiumSetup
{
    private static IOSDriver? _driver;

    public static AppiumDriver? App => _driver;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = "XCUITest",
            PlatformName = "iOS",
            DeviceName = TestConfig.DeviceName ?? "iPhone 16",
        };

        // Leave PlatformVersion unset unless asked for: pinning it to a runtime the runner
        // image doesn't carry fails the session outright, whereas omitting it lets the
        // driver pick whatever simulator runtime is installed.
        if (TestConfig.PlatformVersion is { } version)
        {
            options.PlatformVersion = version;
        }

        if (TestConfig.AppPath is { } appBundle)
        {
            options.App = appBundle;
        }
        else
        {
            // No bundle to install: attach to an already-installed build.
            options.AddAdditionalAppiumOption("bundleId", TestConfig.AppId);
        }

        options.AddAdditionalAppiumOption("noReset", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        // Creating the first session has to build and code-sign WebDriverAgent with Xcode.
        // On a cold CI runner that alone has been measured at over 8 minutes, so these
        // ceilings are deliberately generous — the cost is paid once per run, and the
        // alternative is a timeout that looks like a driver fault rather than a compile.
        // CI additionally pre-builds WDA in its own step so this path is usually fast.
        options.AddAdditionalAppiumOption("wdaLaunchTimeout", 600_000);
        options.AddAdditionalAppiumOption("wdaConnectionTimeout", 600_000);

        _driver = new IOSDriver(TestConfig.ServerUri, options, TimeSpan.FromMinutes(15));
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
