namespace PaycheckCalculator.UITests;

/// <summary>
/// Environment-driven knobs shared by every platform's <c>AppiumSetup</c>.
/// </summary>
/// <remarks>
/// CI builds the app to a path it only knows at run time (and picks whichever simulator or
/// emulator image the runner happens to provide), so nothing here is hard-coded into the
/// platform setups. Locally every value has a working default, so `dotnet test` against an
/// already-deployed app needs no configuration at all.
/// </remarks>
public static class TestConfig
{
    /// <summary>Application id / bundle id. Must match $(ApplicationId) in PaycheckCalculator.App.csproj.</summary>
    public const string AppId = "com.erik.paycheckcalc";

    /// <summary>Fully-qualified Android launcher activity, pinned by [Register] on MainActivity.</summary>
    public const string AndroidMainActivity = AppId + ".MainActivity";

    /// <summary>Appium server the tests talk to.</summary>
    public static Uri ServerUri =>
        new(Environment.GetEnvironmentVariable("APPIUM_SERVER_URI") ?? "http://127.0.0.1:4723/");

    /// <summary>
    /// Path to the built app to install (.apk / .app / .exe). When unset the tests attach to
    /// whatever build is already deployed on the device, which is the usual local workflow.
    /// </summary>
    /// <remarks>
    /// A relative value is resolved against the repository root, not the process working
    /// directory. Under `dotnet test` the working directory is the test assembly's output
    /// folder, so plain <c>Path.GetFullPath</c> turns a repo-relative path like
    /// "PaycheckCalculator.App/bin/…/app.apk" into a nonexistent path nested under
    /// "PaycheckCalculator.UITests.Android/bin/Debug/net11.0/" — and the only symptom is
    /// Appium refusing the session with "does not exist or is not accessible".
    /// </remarks>
    public static string? AppPath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("UITEST_APP_PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            var root = FindRepositoryRoot();
            return root is null
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(root, path));
        }
    }

    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PaycheckCalculator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }

    /// <summary>Target device/simulator name. Platform-specific default applied by the caller.</summary>
    public static string? DeviceName => Get("UITEST_DEVICE_NAME");

    /// <summary>Target OS version, e.g. an iOS Simulator runtime version.</summary>
    public static string? PlatformVersion => Get("UITEST_PLATFORM_VERSION");

    /// <summary>Where failure screenshots are written.</summary>
    public static string ArtifactDirectory =>
        Environment.GetEnvironmentVariable("UITEST_ARTIFACT_DIR")
        ?? Path.Combine(Path.GetTempPath(), "paycheckcalculator-uitests");

    /// <summary>
    /// When true the tests start their own Appium server. CI normally starts one as a
    /// background service instead, so it defaults to off there.
    /// </summary>
    public static bool StartAppiumServer =>
        !string.Equals(Environment.GetEnvironmentVariable("APPIUM_EXTERNAL_SERVER"), "1", StringComparison.Ordinal);

    private static string? Get(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
