using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Shared plumbing for the Appium UI tests: element lookup that papers over the
/// platform differences, polling waits, and a screenshot on failure.
/// </summary>
public abstract class BaseTest
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    protected static AppiumDriver App =>
        AppiumSetup.App ?? throw new InvalidOperationException(
            "The Appium driver was not initialized. Every UI test project must declare a " +
            "[SetUpFixture] named AppiumSetup in the PaycheckCalculator.UITests namespace — " +
            "NUnit only runs a SetUpFixture for fixtures sharing its namespace.");

    /// <summary>
    /// Finds an element by its MAUI <c>AutomationId</c>.
    /// </summary>
    /// <remarks>
    /// WinUI surfaces AutomationId as the UI Automation AutomationId property, which Appium
    /// exposes as an accessibility id. Android, iOS, and Mac Catalyst expose it as the plain
    /// element id. Same XAML attribute, two different locator strategies.
    /// </remarks>
    protected static AppiumElement FindElement(string automationId) =>
        App is WindowsDriver
            ? App.FindElement(MobileBy.AccessibilityId(automationId)) as AppiumElement
              ?? throw new NoSuchElementException(automationId)
            : App.FindElement(MobileBy.Id(automationId)) as AppiumElement
              ?? throw new NoSuchElementException(automationId);

    /// <summary>Waits until an element with the given AutomationId exists and is displayed.</summary>
    protected static AppiumElement WaitForElement(string automationId, TimeSpan? timeout = null) =>
        WaitFor(
            () =>
            {
                var element = FindElement(automationId);
                return element.Displayed ? element : null;
            },
            timeout,
            $"element with AutomationId '{automationId}'");

    /// <summary>
    /// Finds a control by its visible text. Used for the Shell tab bar, whose native tab
    /// items are built by the platform renderer and don't carry the AutomationId we'd set
    /// in XAML — text is the only locator that works consistently across all four backends.
    /// </summary>
    protected static AppiumElement WaitForText(string text, TimeSpan? timeout = null) =>
        WaitFor(
            () =>
            {
                foreach (var by in TextLocators(text))
                {
                    try
                    {
                        if (App.FindElement(by) is AppiumElement { Displayed: true } element)
                        {
                            return element;
                        }
                    }
                    catch (NoSuchElementException)
                    {
                        // Try the next strategy.
                    }
                }

                return null;
            },
            timeout,
            $"element with text '{text}'");

    private static IEnumerable<By> TextLocators(string text)
    {
        if (App is WindowsDriver)
        {
            yield return MobileBy.Name(text);
            yield return MobileBy.AccessibilityId(text);
            yield break;
        }

        yield return MobileBy.AccessibilityId(text);
        // Android exposes label text as @text; iOS/Mac2 expose it as @label or @name.
        yield return MobileBy.XPath($"//*[@text='{text}']");
        yield return MobileBy.XPath($"//*[@label='{text}']");
        yield return MobileBy.XPath($"//*[@name='{text}']");
    }

    /// <summary>Switches to one of the app's five bottom Shell tabs (Inputs, Results, …).</summary>
    protected static void GoToTab(string tabTitle)
    {
        WaitForText(tabTitle).Click();
        // The Shell tab transition is animated; give the destination page a beat to attach
        // before the caller starts querying it.
        Thread.Sleep(500);
    }

    /// <summary>Replaces the contents of a text field rather than appending to it.</summary>
    protected static void SetText(string automationId, string value)
    {
        var element = WaitForElement(automationId);
        element.Clear();
        element.SendKeys(value);
    }

    /// <summary>
    /// Parses a currency-formatted label such as "$1,234.56" into a decimal.
    /// </summary>
    /// <remarks>
    /// The strings come from the device's culture, not the test host's, so this strips
    /// everything that isn't a digit, separator, or sign rather than trusting
    /// <c>decimal.Parse</c> with a fixed culture.
    /// </remarks>
    protected static decimal ParseCurrency(string displayed)
    {
        var cleaned = Regex.Replace(displayed, @"[^\d.,\-]", string.Empty);

        // Whichever separator appears last is the decimal point; the other groups thousands.
        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');
        if (lastComma > lastDot)
        {
            cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
        }
        else
        {
            cleaned = cleaned.Replace(",", string.Empty);
        }

        return decimal.Parse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static AppiumElement WaitFor(
        Func<AppiumElement?> probe,
        TimeSpan? timeout,
        string description)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (probe() is { } element)
                {
                    return element;
                }
            }
            catch (Exception ex) when (ex is NoSuchElementException or StaleElementReferenceException)
            {
                last = ex;
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException(
            $"Timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0}s waiting for {description}.",
            last);
    }

    /// <summary>
    /// Captures a screenshot when a test fails. A UI test that fails without one is very
    /// hard to diagnose from a CI log, since the device is gone by the time anyone looks.
    /// </summary>
    [TearDown]
    public void CaptureScreenshotOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(TestConfig.ArtifactDirectory);
            var name = $"{TestContext.CurrentContext.Test.Name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var path = Path.Combine(TestConfig.ArtifactDirectory, name);
            App.GetScreenshot().SaveAsFile(path);
            TestContext.AddTestAttachment(path, "Screenshot at failure");
            TestContext.Progress.WriteLine($"Failure screenshot: {path}");
        }
        catch (Exception ex)
        {
            // Never let screenshot capture mask the real assertion failure.
            TestContext.Progress.WriteLine($"Could not capture failure screenshot: {ex.Message}");
        }
    }
}
