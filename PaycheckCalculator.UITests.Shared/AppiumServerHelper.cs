using System.Net.Sockets;
using OpenQA.Selenium.Appium.Service;

namespace PaycheckCalculator.UITests;

/// <summary>
/// Starts a local Appium server for the duration of a test run, so `dotnet test` works
/// without a separately-managed terminal.
/// </summary>
/// <remarks>
/// If something is already listening on the configured port — a developer's own
/// <c>appium</c> process, or the background service CI starts — this reuses it rather than
/// failing on a port clash.
/// </remarks>
public static class AppiumServerHelper
{
    private static AppiumLocalService? _service;

    public static void StartAppiumLocalServer()
    {
        if (_service is not null)
        {
            return;
        }

        if (!TestConfig.StartAppiumServer)
        {
            TestContext.Progress.WriteLine(
                $"APPIUM_EXTERNAL_SERVER=1; using the server at {TestConfig.ServerUri}");
            return;
        }

        if (IsPortInUse(TestConfig.ServerUri.Host, TestConfig.ServerUri.Port))
        {
            TestContext.Progress.WriteLine(
                $"Appium already listening on {TestConfig.ServerUri}; reusing it.");
            return;
        }

        TestContext.Progress.WriteLine($"Starting Appium server on {TestConfig.ServerUri} ...");
        _service = new AppiumServiceBuilder()
            .WithIPAddress(TestConfig.ServerUri.Host)
            .UsingPort(TestConfig.ServerUri.Port)
            .Build();
        _service.Start();
    }

    public static void DisposeAppiumLocalServer()
    {
        _service?.Dispose();
        _service = null;
    }

    private static bool IsPortInUse(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            // A short timeout keeps a firewalled or unroutable host from stalling the run;
            // failing the probe just means "start our own server", which is the safe default.
            return client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
