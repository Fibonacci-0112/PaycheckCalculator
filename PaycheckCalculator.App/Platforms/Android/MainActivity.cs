using Android.App;
using Android.Content.PM;
using Android.Runtime;

namespace PaycheckCalculator.App;

// Without [Register], the Android bindings generate a hash-based Java class name
// (crc64…/MainActivity) that changes whenever the namespace does. Appium launches the app
// by fully-qualified activity name, so the UI tests need it pinned and predictable. The
// value must stay in sync with $(ApplicationId) in the .csproj and with the AppActivity
// capability in PaycheckCalculator.UITests.Android/AppiumSetup.cs.
[Register("com.erik.paycheckcalc.MainActivity")]
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density,
    Exported = true)]
public class MainActivity : MauiAppCompatActivity
{
}

