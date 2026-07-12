using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.App.Models;
using PaycheckCalculator.Shared.Snapshots;

namespace PaycheckCalculator.App.ViewModels;

/// <summary>
/// A single calculated paycheck saved for later review and comparison.
/// Wraps the presentation <see cref="ResultCardModel"/> with a user-facing name.
/// The name is the identity used to upsert (re-calculating a paycheck with the
/// same name refreshes its stored result in place).
/// </summary>
public partial class SavedPaycheckViewModel : ObservableObject
{
    public SavedPaycheckViewModel(string name, ResultCardModel result, SavedPaycheckDto snapshot)
    {
        Name = name;
        Result = result;
        Snapshot = snapshot;
    }

    /// <summary>
    /// The serializable snapshot (full input + result numbers) behind this paycheck, used for local
    /// persistence and account sync. Refreshed in place when the paycheck is recalculated.
    /// </summary>
    public SavedPaycheckDto Snapshot { get; set; }

    /// <summary>User-facing label, e.g. "Job 1". Unique (case-insensitive) within the collection.</summary>
    public string Name { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GrossPay))]
    [NotifyPropertyChangedFor(nameof(NetPay))]
    [NotifyPropertyChangedFor(nameof(TotalTaxes))]
    [NotifyPropertyChangedFor(nameof(StateName))]
    public partial ResultCardModel Result { get; set; }

    public decimal GrossPay => Result.GrossPay;
    public decimal NetPay => Result.NetPay;
    public decimal TotalTaxes => Result.TotalTaxes;
    public string StateName => Result.StateName;

    /// <summary>Pickers and lists display the paycheck by its name.</summary>
    public override string ToString() => Name;
}
