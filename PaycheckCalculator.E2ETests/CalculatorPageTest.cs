using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace PaycheckCalculator.E2ETests;

/// <summary>
/// Drives the Blazor calculator in a real browser against a running server.
/// </summary>
/// <remarks>
/// The unit suite covers the tax math; these tests cover what only a live app can prove —
/// that the server starts, the interactive circuit connects, the tax tables are found in
/// the build output's <c>TaxData/</c> folder, and a calculation renders.
/// </remarks>
[Collection(BlazorAppCollection.Name)]
public class CalculatorPageTest : IAsyncLifetime
{
    private readonly BlazorAppFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public CalculatorPageTest(BlazorAppFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = await _fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task CalculatorPageLoadsAndConnectsItsCircuit()
    {
        await GoToCalculatorAsync();

        // The Calculate button is rendered server-side, but it only *responds* once the
        // Blazor Server circuit is live, so an enabled, clickable button means the SignalR
        // connection came up.
        var calculate = _page.GetByTestId("calculate");
        await Assertions.Expect(calculate).ToBeEnabledAsync();
    }

    [Fact]
    public async Task HourlyPaycheckProducesANetPayResult()
    {
        await GoToCalculatorAsync();

        // $40/hr x 40 regular hours + 5 hours of overtime = $1,900 gross.
        await _page.GetByTestId("hourly-rate").FillAsync("40");
        await _page.GetByTestId("regular-hours").FillAsync("40");
        await _page.GetByTestId("overtime-hours").FillAsync("5");

        await _page.GetByTestId("calculate").ClickAsync();

        var netPayLocator = _page.GetByTestId("net-pay");
        await Assertions.Expect(netPayLocator).ToBeVisibleAsync();

        var netPayText = await netPayLocator.InnerTextAsync();
        var netPay = ParseCurrency(netPayText);

        Assert.True(netPay > 0m,
            $"Net pay rendered as '{netPayText}', so the calculation produced no result.");
        Assert.True(netPay < 1900m,
            $"Net pay '{netPayText}' is not below the $1,900 gross — withholding was not applied.");
    }

    [Fact]
    public async Task AnnualTabShowsAProjectionConsistentWithThePerPeriodResult()
    {
        await GoToCalculatorAsync();

        await _page.GetByTestId("hourly-rate").FillAsync("50");
        await _page.GetByTestId("regular-hours").FillAsync("40");
        await _page.GetByTestId("overtime-hours").FillAsync("0");
        await _page.GetByTestId("calculate").ClickAsync();

        var perPeriodNetPay = ParseCurrency(await _page.GetByTestId("net-pay").InnerTextAsync());

        await _page.GetByTestId("result-tab-annual").ClickAsync();

        var annualLocator = _page.GetByTestId("annual-net-pay");
        await Assertions.Expect(annualLocator).ToBeVisibleAsync();
        var annualNetPay = ParseCurrency(await annualLocator.InnerTextAsync());

        // Weekly is the default frequency, so the annualized figure has to be a large
        // multiple of one paycheck. Catches a projection that silently renders zero.
        Assert.True(annualNetPay > perPeriodNetPay * 10,
            $"Annual net pay {annualNetPay} is not consistent with per-period {perPeriodNetPay}.");
    }

    [Fact]
    public async Task NoJavaScriptErrorsDuringACalculation()
    {
        var errors = new List<string>();

        _page.PageError += (_, error) => errors.Add($"page error: {error}");
        _page.Console += (_, message) =>
        {
            if (message.Type != "error")
            {
                return;
            }

            // Skip failed asset requests. This test exists to catch a broken Blazor circuit
            // or an uncaught exception in export.js — the things that make the app stop
            // working. A missing static file is a separate (and visible) concern, and
            // wiring it in here would make every JS regression look like a 404.
            if (message.Text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            errors.Add($"console error: {message.Text}");
        };

        await GoToCalculatorAsync();
        await _page.GetByTestId("calculate").ClickAsync();
        await Assertions.Expect(_page.GetByTestId("net-pay")).ToBeVisibleAsync();

        Assert.True(errors.Count == 0,
            "The browser reported JavaScript errors:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors));
    }

    private async Task GoToCalculatorAsync()
    {
        // The Calculator component has no route of its own; Home.razor hosts it at "/".
        await _page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });
    }

    private static decimal ParseCurrency(string displayed)
    {
        var cleaned = Regex.Replace(displayed, @"[^\d.,\-]", string.Empty);

        // Whichever separator appears last is the decimal point; the other groups thousands.
        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');
        cleaned = lastComma > lastDot
            ? cleaned.Replace(".", string.Empty).Replace(',', '.')
            : cleaned.Replace(",", string.Empty);

        return decimal.Parse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture);
    }
}
