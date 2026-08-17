using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Creates the WinUI (Windows/WinAppDriver) Appium driver for this assembly's tests.
/// </summary>
/// <remarks>
/// <para>
/// Windows only. The Appium windows driver automates through WinAppDriver over the UI
/// Automation framework, so the host needs WinAppDriver 1.2.1 installed and Developer Mode
/// enabled.
/// </para>
/// <para>
/// PaycheckCalculator.App sets <c>WindowsPackageType=None</c>, i.e. the WinUI build is
/// unpackaged. That means the app is launched by executable path rather than by AUMID, so
/// <c>UITEST_APP_PATH</c> must point at PaycheckCalculator.App.exe.
/// </para>
/// </remarks>
[SetUpFixture]
public class AppiumSetup
{
    private static WindowsDriver? _driver;

    public static AppiumDriver? App => _driver;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var appPath = TestConfig.AppPath
            ?? throw new InvalidOperationException(
                "Set UITEST_APP_PATH to the built PaycheckCalculator.App.exe. The WinUI build " +
                "is unpackaged (WindowsPackageType=None), so there is no AUMID to launch by " +
                "and the driver needs the executable path.");

        var options = new AppiumOptions
        {
            AutomationName = "windows",
            PlatformName = "Windows",
            DeviceName = "WindowsPC",
            App = appPath,
        };

        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        // A cold .NET start on a CI runner regularly takes longer than the driver's default.
        options.AddAdditionalAppiumOption("createSessionTimeout", 120_000);

        _driver = new WindowsDriver(TestConfig.ServerUri, options, TimeSpan.FromMinutes(5));
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
