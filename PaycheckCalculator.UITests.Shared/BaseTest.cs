using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Interfaces;
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

    /// <summary>
    /// AutomationId of the always-present root layout on each Shell tab's page. Used to
    /// confirm navigation actually landed, rather than trusting that a click did something.
    /// </summary>
    private static readonly Dictionary<string, string> PageLandmarks = new(StringComparer.Ordinal)
    {
        ["Inputs"] = "Page_Inputs",
        ["Results"] = "Page_Results",
        ["Paychecks"] = "Page_Paychecks",
        ["Budget"] = "Page_Budget",
        ["Account"] = "Page_Account",
    };

    /// <summary>
    /// Switches to one of the app's five bottom Shell tabs (Inputs, Results, …) and waits
    /// until that page is actually on screen.
    /// </summary>
    /// <remarks>
    /// Shell tab items are built by the platform renderer and don't carry a XAML
    /// AutomationId, so they have to be located by their visible text — and a title like
    /// "Results" can match more than one element in the tree. Clicking the wrong match
    /// throws nothing; it simply doesn't navigate, and the failure then surfaces much later
    /// as a missing control on a page the test never reached. So each candidate is clicked
    /// and then checked against the destination page's landmark, moving on to the next
    /// candidate if the app didn't move.
    /// </remarks>
    protected static void GoToTab(string tabTitle)
    {
        if (!PageLandmarks.TryGetValue(tabTitle, out var landmark))
        {
            throw new ArgumentException(
                $"No page landmark registered for tab '{tabTitle}'.", nameof(tabTitle));
        }

        if (IsPresent(landmark))
        {
            return;
        }

        var attempted = 0;
        foreach (var by in TextLocators(tabTitle))
        {
            IReadOnlyCollection<IWebElement> candidates;
            try
            {
                candidates = App.FindElements(by);
            }
            catch (NoSuchElementException)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    if (!candidate.Displayed)
                    {
                        continue;
                    }

                    attempted++;
                    candidate.Click();
                }
                catch (Exception ex) when (ex is ElementNotInteractableException
                                              or StaleElementReferenceException
                                              or InvalidElementStateException)
                {
                    continue;
                }

                // The tab transition is animated, so poll rather than assert immediately.
                if (WaitUntil(() => IsPresent(landmark), TimeSpan.FromSeconds(5)))
                {
                    return;
                }
            }
        }

        throw new TimeoutException(
            $"Could not navigate to the '{tabTitle}' tab: clicked {attempted} matching " +
            $"element(s) but '{landmark}' never appeared.");
    }

    private static bool IsPresent(string automationId)
    {
        try
        {
            return FindElement(automationId).Displayed;
        }
        catch (Exception ex) when (ex is NoSuchElementException or StaleElementReferenceException)
        {
            return false;
        }
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(PollInterval);
        }

        return false;
    }

    /// <summary>
    /// Replaces the contents of a text field and verifies the new value actually took.
    /// </summary>
    /// <remarks>
    /// Entering text is not reliably a single operation across backends. WinUI in particular
    /// can leave <c>Clear()</c> a no-op or drop keystrokes sent to an unfocused field, and
    /// nothing throws when that happens — the field simply keeps its old contents, the
    /// calculation runs on the wrong inputs (or refuses to run at all), and the failure
    /// surfaces much later as a missing result. So this focuses the field first, falls back
    /// to select-all-and-delete when Clear() leaves text behind, and then reads the value
    /// back before returning.
    ///
    /// The read-back compares numerically rather than by string: DecimalFormatBehavior
    /// reformats these fields, so "40" legitimately comes back as "40.00" or "$40.00".
    /// </remarks>
    protected static void SetText(string automationId, string value)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var element = WaitForElement(automationId);

            // Focusing and select-all are WinUI-only on purpose. An unfocused WinUI field
            // silently swallows SendKeys and Clear() can be a no-op there, so both are
            // needed. On Android the same click raises the soft keyboard, which then covers
            // the Calculate button below the form and makes it unfindable — the tests were
            // green there without it. Only pay the cost on the platform that needs it.
            if (App is WindowsDriver)
            {
                try
                {
                    element.Click();
                }
                catch (Exception ex) when (ex is ElementNotInteractableException
                                              or InvalidElementStateException)
                {
                    // Not fatal — fall through and let the read-back below be the judge.
                }

                element.Clear();

                if (!string.IsNullOrEmpty(element.Text))
                {
                    element.SendKeys(Keys.Control + "a");
                    element.SendKeys(Keys.Backspace);
                }
            }
            else
            {
                element.Clear();
            }

            element.SendKeys(value);
            DismissSoftKeyboard();

            if (TextMatches(automationId, value, out var actual))
            {
                return;
            }

            if (attempt == 2)
            {
                throw new InvalidOperationException(
                    $"'{automationId}' still reads '{actual}' after entering '{value}' twice. " +
                    "The field did not accept the input, so anything computed from it would " +
                    "be meaningless.");
            }
        }
    }

    /// <summary>
    /// Closes the on-screen keyboard on the mobile backends.
    /// </summary>
    /// <remarks>
    /// On Android and iOS the keyboard covers the bottom of the screen — which is exactly
    /// where the Shell tab bar and the lower half of the input form live. Anything under it
    /// simply is not in the element tree, so a later lookup fails with "not found" and gives
    /// no hint that a keyboard is the reason. Both iOS calculator tests failed this way:
    /// one could not see the Results tab, the other could not see a field it had just used.
    /// </remarks>
    private static void DismissSoftKeyboard()
    {
        if (App is not IHidesKeyboard keyboard)
        {
            return;
        }

        try
        {
            keyboard.HideKeyboard();
        }
        catch (Exception)
        {
            // Throws when no keyboard is showing, which is the outcome we wanted anyway.
        }
    }

    private static bool TextMatches(string automationId, string expected, out string actual)
    {
        try
        {
            actual = WaitForElement(automationId).Text ?? string.Empty;
        }
        catch (Exception ex) when (ex is NoSuchElementException or TimeoutException)
        {
            actual = "<not found>";
            return false;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        try
        {
            return ParseCurrency(actual) == decimal.Parse(expected, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return false;
        }
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

        var lastSeparator = cleaned.LastIndexOfAny(['.', ',']);
        if (lastSeparator >= 0)
        {
            // Decide whether the final separator is a decimal point or a group separator by
            // how many digits follow it. Currency shows 1-2 decimal places, groups always
            // show exactly 3 — so "$42,116" (rendered with {0:C0}) is forty-two thousand,
            // not forty-two-point-one-one-six.
            var trailingDigits = cleaned.Length - lastSeparator - 1;
            var whole = cleaned[..lastSeparator].Replace(",", string.Empty).Replace(".", string.Empty);
            var rest = cleaned[(lastSeparator + 1)..];
            cleaned = trailingDigits == 3 ? whole + rest : whole + "." + rest;
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
            $"Timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0}s waiting for {description}." +
            Environment.NewLine + DescribeScreen(),
            last);
    }

    /// <summary>
    /// Summarises what is actually on screen, for the failure message.
    /// </summary>
    /// <remarks>
    /// A bare "element not found" says nothing about whether the app is on the wrong page,
    /// showing a validation message, or displaying an error dialog. The screenshot artifact
    /// answers that, but only if somebody downloads it — putting a bounded text dump in the
    /// message itself means the CI log alone is usually enough to tell those apart.
    /// </remarks>
    private static string DescribeScreen()
    {
        const int maxLength = 3000;

        try
        {
            var source = App.PageSource ?? string.Empty;
            return source.Length <= maxLength
                ? $"Screen contents:{Environment.NewLine}{source}"
                : $"Screen contents (first {maxLength} of {source.Length} chars):" +
                  $"{Environment.NewLine}{source[..maxLength]}";
        }
        catch (Exception ex)
        {
            return $"(could not capture screen contents: {ex.Message})";
        }
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
