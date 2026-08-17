using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Creates the Android (UiAutomator2) Appium driver for this assembly's tests.
/// </summary>
/// <remarks>
/// Runs against a booted emulator or an attached device. Host OS: Windows, macOS, or Linux —
/// Android is the only .NET MAUI target that can be built <em>and</em> driven from all three.
/// </remarks>
[SetUpFixture]
public class AppiumSetup
{
    private static AndroidDriver? _driver;

    public static AppiumDriver? App => _driver;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = "UiAutomator2",
            PlatformName = "Android",
            DeviceName = TestConfig.DeviceName ?? "Android Emulator",
        };

        // Always identify the app by package + activity, so the driver attaches to the
        // right process whether Appium installed the APK or CI already did with `adb install`.
        options.AddAdditionalAppiumOption("appPackage", TestConfig.AppId);
        options.AddAdditionalAppiumOption("appActivity", TestConfig.AndroidMainActivity);

        if (TestConfig.AppPath is { } apk)
        {
            options.App = apk;
        }

        // Debug builds can rely on Fast Deployment, whose support files Appium's default
        // reset behaviour deletes; noReset keeps them intact. (This app disables Fast
        // Deployment for Debug via EmbedAssembliesIntoApk, but noReset stays correct either way.)
        options.AddAdditionalAppiumOption("noReset", true);
        // A cold emulator start plus first-run JIT can easily exceed the 60s default.
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("appWaitActivity", "*");
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);

        _driver = new AndroidDriver(TestConfig.ServerUri, options, TimeSpan.FromMinutes(5));
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
