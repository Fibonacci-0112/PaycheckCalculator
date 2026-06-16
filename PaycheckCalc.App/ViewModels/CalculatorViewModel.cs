using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services.Csv;
using PaycheckCalc.App.Services.Pdf;
using PaycheckCalc.App.Services.Printing;
using PaycheckCalc.App.Services.Sync;
using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Shared.Snapshots;
using PaycheckCalc.Shared.Sync;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace PaycheckCalc.App.ViewModels;

public record PickerItem<T>(T Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>
/// How the calculator turns inputs into a result: a standard paycheck (gross → net),
/// a gross-up (target net → required gross), or a bonus / supplemental-wage calculation.
/// </summary>
public enum CalculationMode { Standard, GrossUp, Bonus, SelfEmployment }

public partial class CalculatorViewModel : ObservableObject
{
    private readonly PayCalculator _calc;
    private readonly GrossUpCalculator _grossUp;
    private readonly BonusCalculator _bonus;
    private readonly SelfEmploymentCalculator _selfEmployment;
    private readonly AnnualProjectionCalculator _annual;
    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly IStateSchemaProvider _schemaProvider;
    private readonly IPdfExportService _pdfExport;
    private readonly ICsvExportService _csvExport;
    private readonly IPrintService _printService;
    private readonly ISavedPaycheckStore _store;
    private readonly ISyncCoordinator _sync;
    private UsState _previousState;
    private bool _initialized;

    public CalculatorViewModel(PayCalculator calc, GrossUpCalculator grossUp, BonusCalculator bonus, SelfEmploymentCalculator selfEmployment, AnnualProjectionCalculator annual, StateCalculatorRegistry stateRegistry, IStateSchemaProvider schemaProvider, IPdfExportService pdfExport, ICsvExportService csvExport, IPrintService printService, ISavedPaycheckStore store, ISyncCoordinator sync)
    {
        _calc = calc;
        _grossUp = grossUp;
        _bonus = bonus;
        _selfEmployment = selfEmployment;
        _annual = annual;
        _stateRegistry = stateRegistry;
        _schemaProvider = schemaProvider;
        _pdfExport = pdfExport;
        _csvExport = csvExport;
        _printService = printService;
        _store = store;
        _sync = sync;
        _sync.SyncCompleted += OnSyncCompleted;
        Frequency = PayFrequency.Biweekly;
        SelectedFrequencyPickerItem = Frequencies.FirstOrDefault(f => f.Value == Frequency);
        OvertimeMultiplier = 1.5m;
        SelectedPayTypePickerItem = PayTypes[0];                 // Hourly
        SelectedCalculationModePickerItem = CalculationModes[0]; // Standard
        TargetNetPay = 1000m;
        SelectedGrossPayMethodPickerItem = GrossPayMethods[0];   // Per Year
        SelectedState = UsState.OK;
        _previousState = SelectedState;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);
        SelectedFederalPickerItem = FederalStatuses[0];

        // Build initial dynamic state fields from schema
        RebuildStateFields();

        // Keep computed deduction totals in sync with the collection
        Deductions.CollectionChanged += OnDeductionsCollectionChanged;

        // Keep saved-paycheck computed flags in sync with the collection
        Paychecks.CollectionChanged += OnPaychecksCollectionChanged;
    }

    public ObservableCollection<PickerItem<FederalFilingStatus>> FederalStatuses { get; } = new(
        Enum.GetValues<FederalFilingStatus>()
            .Select(s => new PickerItem<FederalFilingStatus>(s, EnumDisplay.FederalFilingStatus(s.ToString()))));

    [ObservableProperty]
    public partial PickerItem<FederalFilingStatus>? SelectedFederalPickerItem { get; set; }

    partial void OnSelectedFederalPickerItemChanged(PickerItem<FederalFilingStatus>? value)
    {
        if (value != null)
            FederalFilingStatus = value.Value;
    }

    [ObservableProperty]
    public partial PickerItem<PayFrequency>? SelectedFrequencyPickerItem { get; set; }

    partial void OnSelectedFrequencyPickerItemChanged(PickerItem<PayFrequency>? value)
    {
        if (value != null)
            Frequency = value.Value;
    }

    [ObservableProperty] public partial PayFrequency Frequency { get; set; }

    // ── Calculation mode (standard paycheck vs gross-up) ────────
    public IReadOnlyList<PickerItem<CalculationMode>> CalculationModes { get; } =
        Enum.GetValues<CalculationMode>()
            .Select(m => new PickerItem<CalculationMode>(m, EnumDisplay.CalculationMode(m.ToString())))
            .ToList();

    [ObservableProperty] public partial PickerItem<CalculationMode>? SelectedCalculationModePickerItem { get; set; }

    partial void OnSelectedCalculationModePickerItemChanged(PickerItem<CalculationMode>? value)
    {
        if (value != null)
            CalculationMode = value.Value;
    }

    [ObservableProperty] public partial CalculationMode CalculationMode { get; set; } = CalculationMode.Standard;

    partial void OnCalculationModeChanged(CalculationMode value)
    {
        OnPropertyChanged(nameof(IsStandardMode));
        OnPropertyChanged(nameof(IsGrossUpMode));
        OnPropertyChanged(nameof(IsBonusMode));
        OnPropertyChanged(nameof(IsSelfEmploymentMode));
    }

    /// <summary>True when standard paycheck inputs (pay type, hours/salary) should be shown.</summary>
    public bool IsStandardMode => CalculationMode == CalculationMode.Standard;

    /// <summary>True when the gross-up target-net input should be shown.</summary>
    public bool IsGrossUpMode => CalculationMode == CalculationMode.GrossUp;

    /// <summary>True when the bonus / supplemental-wage inputs should be shown.</summary>
    public bool IsBonusMode => CalculationMode == CalculationMode.Bonus;

    /// <summary>True when the self-employment / 1099 input should be shown.</summary>
    public bool IsSelfEmploymentMode => CalculationMode == CalculationMode.SelfEmployment;

    /// <summary>Desired net (take-home) pay for the gross-up calculation.</summary>
    [ObservableProperty] public partial decimal TargetNetPay { get; set; }

    /// <summary>The supplemental payment (bonus) amount for the bonus calculation.</summary>
    [ObservableProperty] public partial decimal BonusAmount { get; set; } = 5000m;

    /// <summary>Supplemental wages already paid this year, for the federal $1,000,000 threshold.</summary>
    [ObservableProperty] public partial decimal YtdSupplementalWages { get; set; }

    /// <summary>Annual net self-employment earnings (Schedule C net profit) for the 1099 calculation.</summary>
    [ObservableProperty] public partial decimal SelfEmploymentEarnings { get; set; } = 80000m;

    // ── Pay type (Hourly vs Salary) ─────────────────────────────
    public IReadOnlyList<PickerItem<PayType>> PayTypes { get; } =
        Enum.GetValues<PayType>()
            .Select(t => new PickerItem<PayType>(t, EnumDisplay.PayType(t.ToString())))
            .ToList();

    [ObservableProperty] public partial PickerItem<PayType>? SelectedPayTypePickerItem { get; set; }

    partial void OnSelectedPayTypePickerItemChanged(PickerItem<PayType>? value)
    {
        if (value != null)
            PayType = value.Value;
    }

    [ObservableProperty] public partial PayType PayType { get; set; } = PayType.Hourly;

    partial void OnPayTypeChanged(PayType value)
    {
        OnPropertyChanged(nameof(IsHourly));
        OnPropertyChanged(nameof(IsSalary));
    }

    /// <summary>True when hourly inputs (pay rate / hours) should be shown.</summary>
    public bool IsHourly => PayType == PayType.Hourly;

    /// <summary>True when salary inputs (gross pay method / amount) should be shown.</summary>
    public bool IsSalary => PayType == PayType.Salary;

    // ── Hourly inputs ───────────────────────────────────────────
    [ObservableProperty] public partial decimal HourlyRate { get; set; }
    [ObservableProperty] public partial decimal RegularHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeMultiplier { get; set; }

    // ── Salary inputs ───────────────────────────────────────────
    [ObservableProperty] public partial decimal SalaryAmount { get; set; }

    public IReadOnlyList<PickerItem<SalaryBasis>> GrossPayMethods { get; } =
        Enum.GetValues<SalaryBasis>()
            .Select(b => new PickerItem<SalaryBasis>(b, EnumDisplay.SalaryBasis(b.ToString())))
            .ToList();

    [ObservableProperty] public partial PickerItem<SalaryBasis>? SelectedGrossPayMethodPickerItem { get; set; }

    partial void OnSelectedGrossPayMethodPickerItemChanged(PickerItem<SalaryBasis>? value)
    {
        if (value != null)
            SalaryBasis = value.Value;
    }

    [ObservableProperty] public partial SalaryBasis SalaryBasis { get; set; } = SalaryBasis.PerYear;

    partial void OnSalaryBasisChanged(SalaryBasis value)
    {
        OnPropertyChanged(nameof(SalaryAmountLabel));
    }

    /// <summary>Label for the salary amount entry, adapting to the chosen gross pay method.</summary>
    public string SalaryAmountLabel => SalaryBasis == SalaryBasis.PerYear ? "Annual Salary" : "Gross Pay Per Period";

    /// <summary>
    /// 1-based paycheck number within the current year for annual projections.
    /// </summary>
    [ObservableProperty] public partial int PaycheckNumber { get; set; } = 1;

    [ObservableProperty] public partial UsState SelectedState { get; set; }

    [ObservableProperty]
    public partial PickerItem<UsState>? SelectedStatePickerItem { get; set; }

    partial void OnSelectedStatePickerItemChanged(PickerItem<UsState>? value)
    {
        if (value != null)
            SelectedState = value.Value;
    }

    /// <summary>
    /// Dynamic state input fields driven by the selected state's schema.
    /// The UI binds to this collection to render the appropriate controls.
    /// </summary>
    public ObservableCollection<StateFieldViewModel> StateFields { get; } = new();

    /// <summary>
    /// State-level validation errors returned by the calculator's <c>Validate</c> method.
    /// </summary>
    [ObservableProperty] public partial ObservableCollection<string> StateValidationErrors { get; set; } = new();

    public bool HasStateValidationErrors => StateValidationErrors.Count > 0;

    partial void OnStateValidationErrorsChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(HasStateValidationErrors));
    }

    /// <summary>True when the selected state has no extra input fields (e.g., no-income-tax states).</summary>
    public bool HasNoStateFields => StateFields.Count == 0;

    /// <summary>
    /// Cache of entered field values keyed by UsState → (fieldKey → rawValue).
    /// Preserves user-entered values when switching between states.
    /// </summary>
    private readonly Dictionary<UsState, Dictionary<string, object?>> _stateFieldCache = new();

    partial void OnSelectedStateChanged(UsState value)
    {
        // Keep the picker item in sync when SelectedState is set programmatically
        if (SelectedStatePickerItem?.Value != value)
            SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == value);
        RebuildStateFields();
    }

    private void RebuildStateFields()
    {
        // Save values for the outgoing state before clearing
        if (StateFields.Count > 0)
        {
            SaveFieldsForState(_previousState);
        }

        StateFields.Clear();
        StateValidationErrors = new ObservableCollection<string>();

        if (_stateRegistry.IsSupported(SelectedState))
        {
            foreach (var field in _schemaProvider.GetSchema(SelectedState))
            {
                var vm = new StateFieldViewModel(field);
                // Restore cached values if available
                if (_stateFieldCache.TryGetValue(SelectedState, out var cache) &&
                    cache.TryGetValue(field.Key, out var cached))
                {
                    switch (field.FieldType)
                    {
                        case StateFieldType.Picker:
                            vm.SelectedOption = cached?.ToString();
                            break;
                        case StateFieldType.Toggle:
                            vm.BoolValue = cached is true;
                            break;
                        default:
                            vm.StringValue = cached?.ToString() ?? "";
                            break;
                    }
                }
                StateFields.Add(vm);
            }
        }

        _previousState = SelectedState;
        OnPropertyChanged(nameof(HasNoStateFields));
    }

    /// <summary>Save field values into the cache for a specific state.</summary>
    private void SaveFieldsForState(UsState state)
    {
        if (StateFields.Count == 0) return;
        var cache = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in StateFields)
        {
            cache[field.Key] = field.Definition.FieldType switch
            {
                StateFieldType.Picker => field.SelectedOption,
                StateFieldType.Toggle => field.BoolValue,
                _ => field.StringValue
            };
        }
        _stateFieldCache[state] = cache;
    }

    /// <summary>
    /// Collection of itemized deductions. Users can add, remove, and edit each entry.
    /// </summary>
    public ObservableCollection<DeductionItemViewModel> Deductions { get; } = new();

    /// <summary>Pre-tax deduction total for display/comparison.</summary>
    public decimal TotalPretaxDeductions =>
        Deductions.Where(d => d.Type == DeductionType.PreTax).Sum(d => d.Amount);

    /// <summary>Post-tax deduction total for display/comparison.</summary>
    public decimal TotalPosttaxDeductions =>
        Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.Amount);

    [RelayCommand]
    private void AddDeduction()
    {
        Deductions.Add(new DeductionItemViewModel());
    }

    [RelayCommand]
    private void RemoveDeduction(DeductionItemViewModel item)
    {
        Deductions.Remove(item);
    }

    private void OnDeductionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (DeductionItemViewModel item in e.NewItems)
                item.PropertyChanged += OnDeductionItemPropertyChanged;

        if (e.OldItems is not null)
            foreach (DeductionItemViewModel item in e.OldItems)
                item.PropertyChanged -= OnDeductionItemPropertyChanged;

        RaiseDeductionTotalsChanged();
    }

    private void OnDeductionItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeductionItemViewModel.Amount)
                           or nameof(DeductionItemViewModel.Type))
        {
            RaiseDeductionTotalsChanged();
        }
    }

    private void RaiseDeductionTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalPretaxDeductions));
        OnPropertyChanged(nameof(TotalPosttaxDeductions));
    }

    // Federal (IRS 15-T / W-4)
    [ObservableProperty]
    public partial FederalFilingStatus FederalFilingStatus { get; set; }
        = FederalFilingStatus.SingleOrMarriedSeparately;

    [ObservableProperty] public partial bool FederalStep2Checked { get; set; }
    [ObservableProperty] public partial decimal FederalStep3Credits { get; set; }
    [ObservableProperty] public partial decimal FederalStep4aOtherIncome { get; set; }
    [ObservableProperty] public partial decimal FederalStep4bDeductions { get; set; }
    [ObservableProperty] public partial decimal FederalStep4cExtraWithholding { get; set; }

    /// <summary>
    /// Presentation-ready result card for the UI — never the raw domain PaycheckResult.
    /// </summary>
    [ObservableProperty] public partial ResultCardModel? ResultCard { get; set; }

    partial void OnResultCardChanged(ResultCardModel? value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowDeductions));
        OnPropertyChanged(nameof(ShowBothDeductions));
        OnPropertyChanged(nameof(ShowResultTabs));
    }

    /// <summary>
    /// Annual projection for the current result, shown on the Results page's Annual sub-tab.
    /// Null for gross-up results (the annual projection applies to standard paychecks only,
    /// mirroring the Blazor app).
    /// </summary>
    [ObservableProperty] public partial AnnualProjectionModel? Projection { get; set; }

    partial void OnProjectionChanged(AnnualProjectionModel? value)
    {
        OnPropertyChanged(nameof(HasAnnual));
        OnPropertyChanged(nameof(ShowResultTabs));
        OnPropertyChanged(nameof(IsPerPeriodTabVisible));
        OnPropertyChanged(nameof(IsAnnualTabVisible));
    }

    /// <summary>True when an annual projection is available (standard, non-gross-up result).</summary>
    public bool HasAnnual => Projection is not null;

    /// <summary>True when the Per Paycheck / Annual sub-tab switcher should be shown.</summary>
    public bool ShowResultTabs => HasResult && HasAnnual;

    /// <summary>Selected results sub-tab: 0 = Per Paycheck, 1 = Annual.</summary>
    [ObservableProperty] public partial int SelectedResultTab { get; set; }

    partial void OnSelectedResultTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsPerPeriodTabVisible));
        OnPropertyChanged(nameof(IsAnnualTabVisible));
        OnPropertyChanged(nameof(IsPerPeriodTabActive));
        OnPropertyChanged(nameof(IsAnnualTabActive));
    }

    /// <summary>True when the per-period results should be shown (default, or whenever there is no annual tab).</summary>
    public bool IsPerPeriodTabVisible => SelectedResultTab == 0 || !HasAnnual;

    /// <summary>True when the annual projection should be shown.</summary>
    public bool IsAnnualTabVisible => SelectedResultTab == 1 && HasAnnual;

    /// <summary>True when the Per Paycheck tab is the active selection (drives tab button styling).</summary>
    public bool IsPerPeriodTabActive => SelectedResultTab == 0;

    /// <summary>True when the Annual tab is the active selection (drives tab button styling).</summary>
    public bool IsAnnualTabActive => SelectedResultTab == 1;

    [RelayCommand]
    private void ShowPerPeriodTab() => SelectedResultTab = 0;

    [RelayCommand]
    private void ShowAnnualTab() => SelectedResultTab = 1;

    /// <summary>True once a paycheck has been calculated, so the results can be shown.</summary>
    public bool HasResult => ResultCard is not null;

    /// <summary>True before any calculation has run; drives the Results-page placeholder.</summary>
    public bool ShowEmptyState => ResultCard is null;

    /// <summary>True when the result has any pre-tax or post-tax deductions to display.</summary>
    public bool ShowDeductions =>
        (ResultCard?.PreTaxDeductions ?? 0m) > 0m || (ResultCard?.PostTaxDeductions ?? 0m) > 0m;

    /// <summary>True when the result has both pre-tax and post-tax deductions (for separator visibility).</summary>
    public bool ShowBothDeductions =>
        (ResultCard?.PreTaxDeductions ?? 0m) > 0m && (ResultCard?.PostTaxDeductions ?? 0m) > 0m;

    /// <summary>
    /// Opens a "Show Your Work" alert for the paycheck line identified by
    /// <paramref name="keyName"/>. Bound from XAML info-icon TapGestureRecognizers
    /// with a CommandParameter naming one of <see cref="ExplanationLineKey"/>.
    /// </summary>
    [RelayCommand]
    private async Task ShowExplanation(string keyName)
    {
        if (ResultCard is null || string.IsNullOrEmpty(keyName)) return;
        if (!Enum.TryParse<ExplanationLineKey>(keyName, out var key)) return;

        var line = ResultCard.Explanation.Get(key);
        if (line is null) return;

        var shell = Shell.Current;
        if (shell is null) return;

        await shell.DisplayAlert(line.Title, FormatExplanation(line), "OK");
    }

    private static string FormatExplanation(LineExplanation line)
    {
        var sb = new StringBuilder();
        var us = CultureInfo.GetCultureInfo("en-US");

        sb.Append("Total: ").Append(line.FinalAmount.ToString("C", us));
        sb.AppendLine();
        sb.AppendLine();

        var i = 1;
        foreach (var step in line.Steps)
        {
            sb.Append(i++).Append(". ").Append(step.Label);
            if (step.Value is not null)
            {
                sb.Append(" — ").Append(step.Value.Value.ToString("C", us));
            }
            sb.AppendLine();
            if (!string.IsNullOrEmpty(step.Detail))
            {
                sb.Append("   ").AppendLine(step.Detail);
            }
            if (!string.IsNullOrEmpty(step.Formula))
            {
                sb.Append("   ").AppendLine(step.Formula);
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(line.Reference))
        {
            sb.Append("Source: ").Append(line.Reference);
        }

        return sb.ToString();
    }

    public IReadOnlyList<PickerItem<PayFrequency>> Frequencies { get; } =
        Enum.GetValues<PayFrequency>()
            .Select(f => new PickerItem<PayFrequency>(f, EnumDisplay.PayFrequency(f.ToString())))
            .ToList();
    public IReadOnlyList<UsState> SupportedStates => _stateRegistry.SupportedStates;

    private IReadOnlyList<PickerItem<UsState>>? _statePickerItems;
    public IReadOnlyList<PickerItem<UsState>> StatePickerItems =>
        _statePickerItems ??= SupportedStates
            .Select(s => new PickerItem<UsState>(s, EnumDisplay.UsStateName(s.ToString())))
            .ToList();

    // ── Saved paychecks & comparison ────────────────────────────

    /// <summary>
    /// Optional name for the paycheck being calculated (e.g. "Job 1"). When blank,
    /// a default "Paycheck N" name is generated. Calculating with an existing name
    /// updates that saved paycheck in place.
    /// </summary>
    [ObservableProperty] public partial string PaycheckName { get; set; } = "";

    /// <summary>All calculated paychecks, stored for review and comparison.</summary>
    public ObservableCollection<SavedPaycheckViewModel> Paychecks { get; } = new();

    /// <summary>True once at least one paycheck has been saved.</summary>
    public bool HasPaychecks => Paychecks.Count > 0;

    /// <summary>True before any paycheck has been saved; drives the empty state.</summary>
    public bool ShowNoPaychecks => Paychecks.Count == 0;

    /// <summary>True with exactly one saved paycheck — prompts the user to add another to compare.</summary>
    public bool ShowCompareHint => Paychecks.Count == 1;

    /// <summary>True once two or more paychecks exist and a comparison can be shown.</summary>
    public bool CanCompare => Paychecks.Count >= 2;

    private void OnPaychecksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPaychecks));
        OnPropertyChanged(nameof(ShowNoPaychecks));
        OnPropertyChanged(nameof(ShowCompareHint));
        OnPropertyChanged(nameof(CanCompare));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ComparisonRows))]
    [NotifyPropertyChangedFor(nameof(ComparisonNameA))]
    public partial SavedPaycheckViewModel? SelectedComparisonA { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ComparisonRows))]
    [NotifyPropertyChangedFor(nameof(ComparisonNameB))]
    public partial SavedPaycheckViewModel? SelectedComparisonB { get; set; }

    /// <summary>True when both comparison slots are filled.</summary>
    public bool HasComparison => SelectedComparisonA is not null && SelectedComparisonB is not null;

    public string ComparisonNameA => SelectedComparisonA?.Name ?? "A";
    public string ComparisonNameB => SelectedComparisonB?.Name ?? "B";

    /// <summary>Per-metric comparison of the two selected paychecks (empty until both are chosen).</summary>
    public IReadOnlyList<ComparisonRow> ComparisonRows => BuildComparisonRows();

    private IReadOnlyList<ComparisonRow> BuildComparisonRows()
    {
        var a = SelectedComparisonA?.Result;
        var b = SelectedComparisonB?.Result;
        if (a is null || b is null)
            return Array.Empty<ComparisonRow>();

        var rows = new List<ComparisonRow>
        {
            new ComparisonRow("Gross Pay", a.GrossPay, b.GrossPay),
            new ComparisonRow("Federal Tax", a.FederalWithholding, b.FederalWithholding),
            new ComparisonRow("Social Security", a.SocialSecurityWithholding, b.SocialSecurityWithholding),
            new ComparisonRow("Medicare",
                a.MedicareWithholding + a.AdditionalMedicareWithholding,
                b.MedicareWithholding + b.AdditionalMedicareWithholding),
            new ComparisonRow("State Income Tax", a.StateWithholding, b.StateWithholding),
        };

        if (a.StateDisabilityInsurance > 0m || b.StateDisabilityInsurance > 0m)
            rows.Add(new ComparisonRow("State Disability", a.StateDisabilityInsurance, b.StateDisabilityInsurance));

        if (a.PreTaxDeductions > 0m || b.PreTaxDeductions > 0m)
            rows.Add(new ComparisonRow("Pre-Tax Deductions", a.PreTaxDeductions, b.PreTaxDeductions));

        if (a.PostTaxDeductions > 0m || b.PostTaxDeductions > 0m)
            rows.Add(new ComparisonRow("Post-Tax Deductions", a.PostTaxDeductions, b.PostTaxDeductions));

        rows.Add(new ComparisonRow("Total Taxes", a.TotalTaxes, b.TotalTaxes));
        rows.Add(new ComparisonRow("Net Pay", a.NetPay, b.NetPay, highlight: true));

        return rows;
    }

    /// <summary>
    /// Stores the just-computed result as a saved paycheck. Re-using an existing
    /// name updates that paycheck in place; otherwise a new entry is added and
    /// auto-selected into the next open comparison slot.
    /// </summary>
    private void SaveCurrentPaycheck(ResultCardModel card, PaycheckInput input)
    {
        var name = string.IsNullOrWhiteSpace(PaycheckName)
            ? $"Paycheck {Paychecks.Count + 1}"
            : PaycheckName.Trim();

        var snapshot = SavedPaycheckSnapshotMapper.ToDto(name, input, card, DateTimeOffset.UtcNow);

        var existing = Paychecks.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Result = card;
            existing.Snapshot = snapshot;
            // Refresh the comparison if this paycheck is currently being compared.
            if (SelectedComparisonA == existing || SelectedComparisonB == existing)
                OnPropertyChanged(nameof(ComparisonRows));
        }
        else
        {
            var saved = new SavedPaycheckViewModel(name, card, snapshot);
            Paychecks.Add(saved);

            // Auto-fill the first open comparison slot for convenience.
            if (SelectedComparisonA is null)
                SelectedComparisonA = saved;
            else if (SelectedComparisonB is null && saved != SelectedComparisonA)
                SelectedComparisonB = saved;
        }

        // Persist locally (works without an account) and sync if signed in.
        PersistAndSync(store => store.UpsertAsync(snapshot));
    }

    /// <summary>
    /// Runs a store operation off the UI thread, swallowing persistence errors (local saving is
    /// best-effort and must never crash the app), then requests a background sync.
    /// </summary>
    private void PersistAndSync(Func<ISavedPaycheckStore, Task> operation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await operation(_store).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort persistence; ignore I/O failures.
            }
            _sync.RequestSync();
        });
    }

    /// <summary>
    /// Loads locally stored paychecks on startup and merges them into the in-memory list (skipping any
    /// names already present), then requests a sync. Idempotent. Call from the UI thread.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        SavedPaycheckSet set;
        try
        {
            set = await _store.LoadAsync().ConfigureAwait(true);
        }
        catch
        {
            return;
        }

        foreach (var dto in set.Paychecks)
        {
            if (Paychecks.Any(p => string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            Paychecks.Add(new SavedPaycheckViewModel(dto.Name, SavedPaycheckSnapshotMapper.ToResultCard(dto), dto));
        }

        SelectedComparisonA ??= Paychecks.FirstOrDefault();
        if (SelectedComparisonB is null)
            SelectedComparisonB = Paychecks.FirstOrDefault(p => p != SelectedComparisonA);

        _sync.RequestSync();
    }

    /// <summary>
    /// Rebuilds the saved-paychecks list from a server-merged set, preserving the current comparison
    /// selections by name. Runs on the UI thread (the coordinator marshals the completion event).
    /// </summary>
    public void ApplyMergedSnapshots(SavedPaycheckSet? merged)
    {
        if (merged is null) return;

        var nameA = SelectedComparisonA?.Name;
        var nameB = SelectedComparisonB?.Name;

        Paychecks.Clear();
        foreach (var dto in merged.Paychecks.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            Paychecks.Add(new SavedPaycheckViewModel(dto.Name, SavedPaycheckSnapshotMapper.ToResultCard(dto), dto));

        SelectedComparisonA = Paychecks.FirstOrDefault(p => string.Equals(p.Name, nameA, StringComparison.OrdinalIgnoreCase));
        SelectedComparisonB = Paychecks.FirstOrDefault(p => string.Equals(p.Name, nameB, StringComparison.OrdinalIgnoreCase));
    }

    private void OnSyncCompleted(object? sender, SyncOutcome outcome)
    {
        if (outcome.Success)
            ApplyMergedSnapshots(outcome.Merged);
    }

    [RelayCommand]
    private async Task RemovePaycheck(SavedPaycheckViewModel? item)
    {
        if (item is null) return;
        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Paycheck", $"Delete \"{item.Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        Paychecks.Remove(item);
        if (SelectedComparisonA == item) SelectedComparisonA = null;
        if (SelectedComparisonB == item) SelectedComparisonB = null;

        // Record a tombstone (even when anonymous) so the delete propagates on a later sign-in.
        var name = item.Name;
        PersistAndSync(store => store.RemoveAsync(name, DateTimeOffset.UtcNow));
    }

    [RelayCommand]
    private void ClearPaychecks()
    {
        Paychecks.Clear();
        SelectedComparisonA = null;
        SelectedComparisonB = null;

        PersistAndSync(store => store.ClearAsync(DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Loads a saved paycheck's stored <see cref="PaycheckInput"/> back into the calculator form
    /// and switches to the Inputs tab so the user can review or tweak it. The paycheck's name is
    /// restored too, so re-calculating updates that saved entry in place. Saved gross-up paychecks
    /// reload as a standard paycheck (the snapshot stores the resolved forward input).
    /// </summary>
    [RelayCommand]
    private async Task LoadPaycheck(SavedPaycheckViewModel? item)
    {
        if (item is null) return;
        var input = item.Snapshot.Input;

        // Restore the name identity so a later Calculate upserts this same paycheck.
        PaycheckName = item.Name;

        // Reload as a standard paycheck and restore the pay basics via their picker items
        // (setting the picker cascades to the underlying value and updates the visible selection).
        SelectedCalculationModePickerItem = CalculationModes.FirstOrDefault(m => m.Value == CalculationMode.Standard);
        SelectedFrequencyPickerItem = Frequencies.FirstOrDefault(f => f.Value == input.Frequency);
        SelectedPayTypePickerItem = PayTypes.FirstOrDefault(t => t.Value == input.PayType);
        SelectedGrossPayMethodPickerItem = GrossPayMethods.FirstOrDefault(b => b.Value == input.SalaryBasis);

        HourlyRate = input.HourlyRate;
        RegularHours = input.RegularHours;
        OvertimeHours = input.OvertimeHours;
        OvertimeMultiplier = input.OvertimeMultiplier;
        SalaryAmount = input.SalaryAmount;
        PaycheckNumber = input.PaycheckNumber;

        // Federal W-4
        SelectedFederalPickerItem = FederalStatuses.FirstOrDefault(s => s.Value == input.FederalW4.FilingStatus);
        FederalStep2Checked = input.FederalW4.Step2Checked;
        FederalStep3Credits = input.FederalW4.Step3TaxCredits;
        FederalStep4aOtherIncome = input.FederalW4.Step4aOtherIncome;
        FederalStep4bDeductions = input.FederalW4.Step4bDeductions;
        FederalStep4cExtraWithholding = input.FederalW4.Step4cExtraWithholding;

        // State — setting SelectedState rebuilds the dynamic StateFields from the schema (with
        // defaults); then overwrite each field from the stored values.
        SelectedState = input.State;
        ApplyStateInputValues(input.StateInputValues);

        // Deductions — rebuild the editable list from the stored deductions.
        Deductions.Clear();
        foreach (var d in input.Deductions)
            Deductions.Add(ToDeductionItem(d));

        // Surface the populated form. Navigation is best-effort — the form is already
        // loaded, so a routing hiccup must not surface as a command failure.
        try
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//Inputs");
        }
        catch
        {
            // Ignore navigation failures; the inputs are populated regardless.
        }
    }

    /// <summary>Writes stored state field values onto the currently built <see cref="StateFields"/>.</summary>
    private void ApplyStateInputValues(StateInputValues? values)
    {
        if (values is null) return;
        foreach (var field in StateFields)
        {
            if (!values.TryGetValue(field.Key, out var raw) || raw is null)
                continue;

            switch (field.Definition.FieldType)
            {
                case StateFieldType.Picker:
                    field.SelectedOption = raw.ToString();
                    break;
                case StateFieldType.Toggle:
                    field.BoolValue = raw is bool b
                        ? b
                        : bool.TryParse(raw.ToString(), out var parsed) && parsed;
                    break;
                default:
                    field.StringValue = raw.ToString() ?? "";
                    break;
            }
        }
    }

    /// <summary>Rebuilds an editable deduction row from a stored domain <see cref="Deduction"/>.</summary>
    private static DeductionItemViewModel ToDeductionItem(Deduction d)
    {
        var item = new DeductionItemViewModel
        {
            Name = d.Name,
            Amount = d.Amount,
            AmountType = d.AmountType,
            ReducesFederalTaxableWages = d.ReducesFederalTaxableWages,
            ReducesStateTaxableWages = d.ReducesStateTaxableWages,
            ReducesFicaWages = d.ReducesFicaWages
        };
        item.SelectedDeductionTypePickerItem =
            item.DeductionTypeItems.FirstOrDefault(t => t.Value == d.Type);
        return item;
    }

    [RelayCommand]
    private void Calculate()
    {
        // Bonus mode is self-contained: flat supplemental withholding + FICA + state
        // supplemental rate. It ignores hours/salary, W-4, and deductions, so it skips the
        // paycheck-specific validation and state-schema fields below.
        if (IsBonusMode)
        {
            CalculateBonus();
            return;
        }

        // Require a name on every deduction before proceeding.
        foreach (var d in Deductions) d.HasNameError = false;
        var unnamed = Deductions.Where(d => string.IsNullOrWhiteSpace(d.Name)).ToList();
        if (unnamed.Count > 0 && !IsSelfEmploymentMode)
        {
            foreach (var d in unnamed) d.HasNameError = true;
            return;
        }

        // Build dynamic state input values from the schema-driven fields
        var stateValues = new StateInputValues();
        foreach (var field in StateFields)
            stateValues[field.Key] = field.GetResolvedValue();

        // Run local field-level validation on each state field
        bool hasFieldErrors = false;
        foreach (var field in StateFields)
        {
            field.Validate();
            if (field.HasError) hasFieldErrors = true;
        }

        // Run state calculator's Validate(...) for cross-field / business rules
        var stateErrors = new List<string>();
        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            stateErrors.AddRange(calc.Validate(stateValues));
        }
        StateValidationErrors = new ObservableCollection<string>(stateErrors);

        // Block calculation when state input is invalid
        if (hasFieldErrors || stateErrors.Count > 0)
            return;

        // Self-employment mode reuses the validated state inputs but its own engine.
        if (IsSelfEmploymentMode)
        {
            CalculateSelfEmployment(stateValues);
            return;
        }

        // Map ViewModel state → domain input via mapper
        var input = PaycheckInputMapper.Map(this, stateValues);

        // Run the calculation for the selected mode and map to the presentation model.
        if (IsGrossUpMode)
        {
            var grossUpResult = _grossUp.Calculate(input, TargetNetPay);
            ResultCard = ResultCardMapper.MapGrossUp(grossUpResult);
            // The annual projection applies to standard paychecks only.
            Projection = null;
        }
        else
        {
            var domainResult = _calc.Calculate(input);
            ResultCard = ResultCardMapper.Map(domainResult);
            Projection = AnnualProjectionMapper.Map(_annual.Calculate(input, domainResult));
        }

        // Always land on the Per Paycheck sub-tab after a recalculation.
        SelectedResultTab = 0;

        // Store the result so multiple paychecks can be kept and compared.
        SaveCurrentPaycheck(ResultCard, input);

        // Prefill the export file name from the paycheck name (the user can edit it).
        ExportFileName = PaycheckName.Trim();

        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Runs the bonus / supplemental-wage calculation and maps it to the result card. Bonus
    /// results have no annual projection (Projection = null) and are not added to the saved
    /// paychecks list, mirroring how gross-up keeps the per-period view focused.
    /// </summary>
    private void CalculateBonus()
    {
        var bonusResult = _bonus.Calculate(new BonusInput
        {
            BonusAmount = BonusAmount,
            State = SelectedState,
            YtdSupplementalWages = YtdSupplementalWages
        });

        ResultCard = ResultCardMapper.MapBonus(bonusResult);
        Projection = null;
        SelectedResultTab = 0;

        // Prefill the export file name from the paycheck name (the user can edit it).
        ExportFileName = string.IsNullOrWhiteSpace(PaycheckName) ? "Bonus-Summary" : PaycheckName.Trim();

        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Runs the self-employment / 1099 calculation and maps it to the result card. Like the
    /// bonus path it has no annual projection (Projection = null) and is not added to the
    /// saved-paychecks list; it reuses the validated <paramref name="stateValues"/> for the
    /// state income-tax estimate.
    /// </summary>
    private void CalculateSelfEmployment(StateInputValues stateValues)
    {
        var seResult = _selfEmployment.Calculate(new SelfEmploymentInput
        {
            AnnualNetEarnings = SelfEmploymentEarnings,
            State = SelectedState,
            StateInputValues = stateValues
        });

        ResultCard = ResultCardMapper.MapSelfEmployment(seResult);
        Projection = null;
        SelectedResultTab = 0;

        // Prefill the export file name from the paycheck name (the user can edit it).
        ExportFileName = string.IsNullOrWhiteSpace(PaycheckName) ? "Self-Employment-Summary" : PaycheckName.Trim();

        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Editable base file name for exports, prefilled from the paycheck name on each
    /// calculation. Blank falls back to "Paycheck-Summary" at export time.
    /// </summary>
    [ObservableProperty] public partial string ExportFileName { get; set; } = "";

    /// <summary>Comparison rows to append to the main export, or null when no A/B pair is selected.</summary>
    private IReadOnlyList<ComparisonRow>? ComparisonForExport() => HasComparison ? ComparisonRows : null;

    /// <summary>True once a paycheck has been calculated and can be exported.</summary>
    public bool CanExportPdf => ResultCard is not null;

    /// <summary>
    /// Exports the current results to a PDF and opens it in Adobe Reader/Acrobat. Includes the
    /// annual projection and, when an A/B pair is selected, the comparison table.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportPdf))]
    private async Task ExportPdf()
    {
        if (ResultCard is null)
            return;

        try
        {
            var bytes = PaycheckPdfRenderer.Render(ResultCard, Projection, ComparisonForExport(), ComparisonNameA, ComparisonNameB);
            await _pdfExport.ExportAndOpenAsync(bytes, ExportFileName);
        }
        catch (Exception ex)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Export PDF", $"Could not export the PDF: {ex.Message}", "OK");
        }
    }

    /// <summary>True once a paycheck has been calculated and can be exported.</summary>
    public bool CanExportCsv => ResultCard is not null;

    /// <summary>
    /// Exports the current results to CSV and opens it in the platform's default CSV application.
    /// Includes the annual projection and, when selected, the comparison table.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsv()
    {
        if (ResultCard is null)
            return;

        try
        {
            var csv = PaycheckCsvRenderer.Render(ResultCard, Projection, ComparisonForExport(), ComparisonNameA, ComparisonNameB);
            await _csvExport.ExportAndOpenAsync(csv, ExportFileName);
        }
        catch (Exception ex)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Export CSV", $"Could not export the CSV: {ex.Message}", "OK");
        }
    }

    /// <summary>True once a paycheck has been calculated and can be printed.</summary>
    public bool CanPrint => ResultCard is not null;

    /// <summary>Sends the current results (with annual projection and any comparison) to the print system.</summary>
    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task Print()
    {
        if (ResultCard is null)
            return;

        try
        {
            var bytes = PaycheckPdfRenderer.Render(ResultCard, Projection, ComparisonForExport(), ComparisonNameA, ComparisonNameB);
            await _printService.PrintAsync(bytes, ExportFileName);
        }
        catch (Exception ex)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Print", $"Could not print the results: {ex.Message}", "OK");
        }
    }

    // ── Dedicated A/B comparison export ─────────────────────────

    /// <summary>True when two paychecks are selected and the comparison can be exported on its own.</summary>
    public bool CanExportComparison => HasComparison;

    partial void OnSelectedComparisonAChanged(SavedPaycheckViewModel? value)
    {
        if (value is not null && ReferenceEquals(value, SelectedComparisonB))
            SelectedComparisonB = Paychecks.FirstOrDefault(p => !ReferenceEquals(p, value));
        RefreshComparisonExportState();
    }

    partial void OnSelectedComparisonBChanged(SavedPaycheckViewModel? value)
    {
        if (value is not null && ReferenceEquals(value, SelectedComparisonA))
            SelectedComparisonA = Paychecks.FirstOrDefault(p => !ReferenceEquals(p, value));
        RefreshComparisonExportState();
    }

    private void RefreshComparisonExportState()
    {
        ExportComparisonPdfCommand.NotifyCanExecuteChanged();
        ExportComparisonCsvCommand.NotifyCanExecuteChanged();
    }

    private string ComparisonExportFileName() => $"Comparison-{ComparisonNameA}-vs-{ComparisonNameB}";

    /// <summary>Exports just the A/B comparison table to a standalone PDF.</summary>
    [RelayCommand(CanExecute = nameof(CanExportComparison))]
    private async Task ExportComparisonPdf()
    {
        if (!HasComparison)
            return;

        try
        {
            var bytes = PaycheckPdfRenderer.RenderComparison(ComparisonRows, ComparisonNameA, ComparisonNameB);
            await _pdfExport.ExportAndOpenAsync(bytes, ComparisonExportFileName());
        }
        catch (Exception ex)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Export Comparison", $"Could not export the comparison PDF: {ex.Message}", "OK");
        }
    }

    /// <summary>Exports just the A/B comparison table to a standalone CSV.</summary>
    [RelayCommand(CanExecute = nameof(CanExportComparison))]
    private async Task ExportComparisonCsv()
    {
        if (!HasComparison)
            return;

        try
        {
            var csv = PaycheckCsvRenderer.RenderComparison(ComparisonRows, ComparisonNameA, ComparisonNameB);
            await _csvExport.ExportAndOpenAsync(csv, ComparisonExportFileName());
        }
        catch (Exception ex)
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Export Comparison", $"Could not export the comparison CSV: {ex.Message}", "OK");
        }
    }
}
