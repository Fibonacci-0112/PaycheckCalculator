using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalculator.App.Services.Storage;
using PaycheckCalculator.App.Services.Sync;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Client;
using PaycheckCalculator.Shared.Snapshots;
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
    private readonly ISavedPaycheckStore _paycheckStore;
    private readonly IBudgetStore _budgetStore;
    private readonly IJsonExportService _jsonExport;
    private bool _initialized;

    public AccountViewModel(
        PaycheckApiClient api,
        ISyncCoordinator sync,
        PreferencesApiBaseAddressProvider serverUrl,
        ISavedPaycheckStore paycheckStore,
        IBudgetStore budgetStore,
        IJsonExportService jsonExport)
    {
        _api = api;
        _sync = sync;
        _serverUrl = serverUrl;
        _paycheckStore = paycheckStore;
        _budgetStore = budgetStore;
        _jsonExport = jsonExport;
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

    [RelayCommand]
    private Task ExportAccountDataAsync() => RunAsync(async () =>
    {
        if (!IsSignedIn)
        {
            StatusMessage = "Sign in first to export your synced account data.";
            return;
        }

        var result = await _api.ExportAccountDataAsync();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Value))
        {
            StatusMessage = result.Error ?? "Could not export account data.";
            return;
        }

        await _jsonExport.ExportAndOpenAsync(result.Value, $"paycheckcalc-account-export-{DateTime.UtcNow:yyyyMMdd}");
        StatusMessage = "Account data exported as JSON.";
    });

    [RelayCommand]
    private Task DeleteLocalDataAsync() => RunAsync(async () =>
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Delete Local Data",
            "Delete all local paychecks and budget data from this device? This will not delete cloud account data.",
            "Delete",
            "Cancel");
        if (!confirmed) return;

        await ClearLocalDataAsync();
        StatusMessage = "Local paycheck and budget data deleted from this device.";
    });

    [RelayCommand]
    private Task DeleteAccountAsync() => RunAsync(async () =>
    {
        if (!IsSignedIn)
        {
            StatusMessage = "Sign in first to delete your account.";
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            "Delete Account",
            "Delete your account and all synced cloud data permanently? This cannot be undone.",
            "Delete Account",
            "Cancel");
        if (!confirmed) return;

        var result = await _api.DeleteAccountAsync();
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Could not delete account.";
            return;
        }

        await ClearLocalDataAsync();
        IsSignedIn = false;
        Password = "";
        StatusMessage = "Account and synced cloud data deleted.";
    });

    [RelayCommand]
    private Task ViewPrivacyPolicyAsync() => RunAsync(async () =>
    {
        await Shell.Current.DisplayAlert(
            "Privacy Policy",
            "PaycheckCalc stores data locally by default. If you create an account, synced paychecks and budgets are stored on the sync server so your data can sync across devices. Use Export Account Data, Delete Local Data, or Delete Account at any time.",
            "OK");
    });

    [RelayCommand]
    private Task ViewTermsAsync() => RunAsync(async () =>
    {
        await Shell.Current.DisplayAlert(
            "Terms of Use",
            "PaycheckCalc is provided for informational purposes only and is not tax advice. You are responsible for reviewing payroll and tax outcomes before relying on them. Use of account sync features requires network access and acceptance of server-side data storage.",
            "OK");
    });

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

    private async Task ClearLocalDataAsync()
    {
        await _paycheckStore.ReplaceAllAsync(new SavedPaycheckSet([], []));
        await _budgetStore.ReplaceAllBudgetsAsync(new BudgetSet([], []));
        await _budgetStore.ReplaceAllTransactionsAsync(new TransactionSet([], []));
        await _budgetStore.ReplaceAllRecurringBillsAsync(new RecurringBillSet([], []));
        await _budgetStore.ReplaceAllSavingsGoalsAsync(new SavingsGoalSet([], []));
    }
}
