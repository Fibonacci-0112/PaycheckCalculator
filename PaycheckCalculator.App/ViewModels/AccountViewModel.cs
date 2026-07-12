using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalculator.App.Services.Sync;
using PaycheckCalculator.Shared.Client;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.App.ViewModels;

/// <summary>
/// Backs the Account page: optional email/password sign-in, account creation, manual sync, sign-out,
/// and an editable server URL. The sync itself is delegated to <see cref="ISyncCoordinator"/>; the
/// merged result is applied to the saved-paychecks list by <see cref="CalculatorViewModel"/>, which
/// listens to the same <see cref="ISyncCoordinator.SyncCompleted"/> event.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private readonly PaycheckApiClient _api;
    private readonly ISyncCoordinator _sync;
    private readonly PreferencesApiBaseAddressProvider _serverUrl;
    private bool _initialized;

    public AccountViewModel(PaycheckApiClient api, ISyncCoordinator sync, PreferencesApiBaseAddressProvider serverUrl)
    {
        _api = api;
        _sync = sync;
        _serverUrl = serverUrl;
        ServerUrl = serverUrl.ServerUrl;
        _sync.SyncCompleted += OnSyncCompleted;
    }

    [ObservableProperty] public partial string Email { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";
    [ObservableProperty] public partial string ServerUrl { get; set; } = PreferencesApiBaseAddressProvider.DefaultServerUrl;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedOut))]
    public partial bool IsSignedIn { get; set; }

    public bool IsSignedOut => !IsSignedIn;

    /// <summary>Loads the initial signed-in state. Safe to call repeatedly (runs once).</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        IsSignedIn = await _sync.IsSignedInAsync();
    }

    partial void OnServerUrlChanged(string value) => _serverUrl.ServerUrl = value;

    [RelayCommand]
    private Task LoginAsync() => RunAsync(async () =>
    {
        var result = await _api.LoginAsync(Email.Trim(), Password);
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Sign in failed.";
            return;
        }
        Password = "";
        IsSignedIn = true;
        await _sync.SyncNowAsync();
    });

    [RelayCommand]
    private Task RegisterAsync() => RunAsync(async () =>
    {
        var register = await _api.RegisterAsync(Email.Trim(), Password);
        if (!register.Success)
        {
            StatusMessage = register.Error ?? "Could not create the account.";
            return;
        }
        var login = await _api.LoginAsync(Email.Trim(), Password);
        if (!login.Success)
        {
            StatusMessage = login.Error ?? "Account created, but sign in failed.";
            return;
        }
        Password = "";
        IsSignedIn = true;
        await _sync.SyncNowAsync();
    });

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        IsSignedIn = false;
        StatusMessage = "Signed out. Your paychecks remain saved on this device.";
    }

    [RelayCommand]
    private Task SyncNowAsync() => RunAsync(async () => await _sync.SyncNowAsync());

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSyncCompleted(object? sender, SyncOutcome outcome)
    {
        StatusMessage = outcome.Success
            ? $"Synced {(outcome.Merged?.Paychecks.Count ?? 0)} paycheck(s)."
            : outcome.Error ?? "Sync failed.";
    }
}
