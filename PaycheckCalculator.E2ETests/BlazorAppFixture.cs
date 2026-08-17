using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace PaycheckCalculator.E2ETests;

/// <summary>
/// Provides a running Blazor server and a Playwright browser for the end-to-end tests.
/// </summary>
/// <remarks>
/// Points at an already-running server when <c>E2E_BASE_URL</c> is set — how CI does it, so
/// the app under test is the same published build that would ship. With no base URL the
/// fixture starts <c>dotnet run</c> itself, which keeps `dotnet test` a one-liner locally.
/// </remarks>
public sealed class BlazorAppFixture : IAsyncLifetime
{
    private Process? _server;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var externalUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            BaseUrl = externalUrl.TrimEnd('/');
        }
        else
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _server = StartBlazorServer(BaseUrl);
        }

        await WaitForServerAsync(BaseUrl);

        _playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            // Headed runs are occasionally useful when debugging a failing selector locally.
            Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1",
        };

        // Playwright pins an exact Chromium build per release and refuses to start against
        // any other. On a machine whose pre-installed browsers were provisioned for a
        // different Playwright version, point this at a compatible binary instead of
        // re-downloading one.
        var executablePath = Environment.GetEnvironmentVariable("E2E_BROWSER_EXECUTABLE");
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            launchOptions.ExecutablePath = executablePath;
        }

        Browser = await _playwright.Chromium.LaunchAsync(launchOptions);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            _server.WaitForExit(10_000);
        }

        _server?.Dispose();
    }

    private static Process StartBlazorServer(string baseUrl)
    {
        var repoRoot = FindRepositoryRoot();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("PaycheckCalculator.Blazor");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
        // Development skips the HTTPS redirect, which would otherwise bounce the plain-HTTP
        // test client to a port nothing is listening on.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Blazor server.");

        // Drain both pipes; a full buffer would deadlock the child process.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static async Task WaitForServerAsync(string baseUrl)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(3);
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync(baseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                last = ex;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"The Blazor app at {baseUrl} did not become ready within 3 minutes.", last);
    }

    private static int GetFreePort()
    {
        // Binding port 0 lets the OS hand out a free port; released immediately so Kestrel
        // can claim it. A parallel job could theoretically take it in between, but the
        // alternative — a fixed port — collides far more often on shared CI runners.
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PaycheckCalculator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the repository root (no PaycheckCalculator.slnx found above " +
                $"{AppContext.BaseDirectory}). Set E2E_BASE_URL to test an already-running server.");
    }
}

[CollectionDefinition(Name)]
public sealed class BlazorAppCollection : ICollectionFixture<BlazorAppFixture>
{
    public const string Name = "blazor-app";
}
