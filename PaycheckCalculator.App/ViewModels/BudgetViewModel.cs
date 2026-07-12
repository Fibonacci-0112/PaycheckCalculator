using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalculator.App.Services.Csv;
using PaycheckCalculator.Core.Budgeting;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Entitlements;
using System.Collections.ObjectModel;

namespace PaycheckCalculator.App.ViewModels;

/// <summary>
/// ViewModel for the Budget tab. Manages categories (including the 50/30/20 preset), expense
/// transactions, and per-category summary (budgeted vs spent vs projected). Persists via
/// <see cref="IBudgetStore"/> and delegates calculation to <see cref="BudgetCalculator"/>.
/// </summary>
public partial class BudgetViewModel : ObservableObject
{
    private readonly BudgetCalculator _calc;
    private readonly IBudgetStore _store;
    private readonly CalculatorViewModel _calculator;
    private readonly BudgetReportCalculator _reportCalc;
    private readonly IEntitlementProvider _entitlements;
    private readonly ICsvExportService _csvExport;
    private bool _initialized;

    public BudgetViewModel(
        BudgetCalculator calc,
        IBudgetStore store,
        CalculatorViewModel calculator,
        BudgetReportCalculator reportCalc,
        IEntitlementProvider entitlements,
        ICsvExportService csvExport)
    {
        _calc = calc;
        _store = store;
        _calculator = calculator;
        _reportCalc = reportCalc;
        _entitlements = entitlements;
        _csvExport = csvExport;
    }

    // ── Identity ─────────────────────────────────────────────────────────────

    [ObservableProperty] public partial string BudgetName { get; set; } = "My Budget";

    // ── Monthly income ────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIncome))]
    public partial decimal MonthlyNetIncome { get; set; }

    public bool HasIncome => MonthlyNetIncome > 0m;

    // ── Budgeting method ────────────────────────────────────────────────────────

    [ObservableProperty] public partial BudgetMethod Method { get; set; } = BudgetMethod.FiftyThirtyTwenty;

    /// <summary>Methods offered in the picker.</summary>
    public IReadOnlyList<BudgetMethod> Methods { get; } =
        [BudgetMethod.FiftyThirtyTwenty, BudgetMethod.ZeroBased, BudgetMethod.Envelope, BudgetMethod.Custom];

    // ── Categories ────────────────────────────────────────────────────────────

    public ObservableCollection<BudgetCategoryViewModel> Categories { get; } = new();

    public bool HasCategories => Categories.Count > 0;

    partial void OnMonthlyNetIncomeChanged(decimal value) => RefreshSummary();

    /// <summary>
    /// Seeds categories from the selected <see cref="Method"/>'s template, using the most recently
    /// calculated paycheck's net pay and frequency as the monthly income basis. Custom leaves the
    /// existing categories in place (the user builds them manually).
    /// </summary>
    [RelayCommand]
    private void ApplyMethodTemplate()
    {
        var resultCard = _calculator.ResultCard;
        if (resultCard is null) return;

        var monthly = MonthlyIncomeNormalizer.ToMonthly(resultCard.NetPay, _calculator.Frequency);
        MonthlyNetIncome = monthly;

        var preset = AllocationRules.ForMethod(Method);
        if (preset.Count > 0)
        {
            Categories.Clear();
            foreach (var cat in preset)
            {
                Categories.Add(new BudgetCategoryViewModel
                {
                    Name = cat.Name,
                    BudgetType = cat.BudgetType,
                    Amount = cat.Amount,
                    AmountType = cat.AmountType,
                    Budgeted = cat.EffectiveMonthlyBudget(monthly)
                });
            }
        }

        OnPropertyChanged(nameof(HasCategories));
        RefreshSummary();
        PersistBudget();
    }

    [RelayCommand]
    private void AddCategory()
    {
        Categories.Add(new BudgetCategoryViewModel { Name = "New Category", Amount = 0m });
        OnPropertyChanged(nameof(HasCategories));
        PersistBudget();
    }

    [RelayCommand]
    private void RemoveCategory(BudgetCategoryViewModel? item)
    {
        if (item is null) return;
        Categories.Remove(item);
        OnPropertyChanged(nameof(HasCategories));
        RefreshSummary();
        PersistBudget();
    }

    // ── Transactions ──────────────────────────────────────────────────────────

    public ObservableCollection<BudgetTransactionViewModel> Transactions { get; } = new();

    public bool HasTransactions => Transactions.Count > 0;

    [ObservableProperty] public partial string NewTransactionCategory { get; set; } = "";
    [ObservableProperty] public partial decimal NewTransactionAmount { get; set; }
    [ObservableProperty] public partial string NewTransactionDescription { get; set; } = "";

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTransactionCategory) || NewTransactionAmount <= 0m) return;

        var vm = new BudgetTransactionViewModel
        {
            Id = Guid.NewGuid(),
            CategoryName = NewTransactionCategory.Trim(),
            Amount = NewTransactionAmount,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Description = NewTransactionDescription.Trim()
        };
        Transactions.Insert(0, vm);
        OnPropertyChanged(nameof(HasTransactions));

        NewTransactionCategory = "";
        NewTransactionAmount = 0m;
        NewTransactionDescription = "";

        RefreshSummary();
        await PersistTransactionAsync(vm);
    }

    [RelayCommand]
    private async Task RemoveTransactionAsync(BudgetTransactionViewModel? item)
    {
        if (item is null) return;
        Transactions.Remove(item);
        OnPropertyChanged(nameof(HasTransactions));
        RefreshSummary();

        try
        {
            await _store.RemoveTransactionAsync(item.Id, DateTimeOffset.UtcNow).ConfigureAwait(false);
        }
        catch { }
    }

    // ── Recurring bills ───────────────────────────────────────────────────────

    public ObservableCollection<RecurringBillViewModel> RecurringBills { get; } = new();

    public bool HasRecurringBills => RecurringBills.Count > 0;

    [ObservableProperty] public partial string NewBillName { get; set; } = "";
    [ObservableProperty] public partial string NewBillCategory { get; set; } = "";
    [ObservableProperty] public partial decimal NewBillAmount { get; set; }
    [ObservableProperty] public partial RecurrenceFrequency NewBillFrequency { get; set; } = RecurrenceFrequency.Monthly;

    public IReadOnlyList<RecurrenceFrequency> Frequencies { get; } = Enum.GetValues<RecurrenceFrequency>();

    [RelayCommand]
    private async Task AddBillAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBillName) || string.IsNullOrWhiteSpace(NewBillCategory) || NewBillAmount <= 0m) return;

        var vm = new RecurringBillViewModel
        {
            Id = Guid.NewGuid(),
            Name = NewBillName.Trim(),
            CategoryName = NewBillCategory.Trim(),
            Amount = NewBillAmount,
            Frequency = NewBillFrequency
        };
        RecurringBills.Add(vm);
        OnPropertyChanged(nameof(HasRecurringBills));

        NewBillName = "";
        NewBillCategory = "";
        NewBillAmount = 0m;
        NewBillFrequency = RecurrenceFrequency.Monthly;

        RefreshSummary();
        await PersistBillAsync(vm).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveBillAsync(RecurringBillViewModel? item)
    {
        if (item is null) return;
        RecurringBills.Remove(item);
        OnPropertyChanged(nameof(HasRecurringBills));
        RefreshSummary();

        try { await _store.RemoveRecurringBillAsync(item.Id, DateTimeOffset.UtcNow).ConfigureAwait(false); }
        catch { }
    }

    // ── Savings goals ─────────────────────────────────────────────────────────

    public ObservableCollection<SavingsGoalViewModel> SavingsGoals { get; } = new();

    public bool HasSavingsGoals => SavingsGoals.Count > 0;

    [ObservableProperty] public partial string NewGoalName { get; set; } = "";
    [ObservableProperty] public partial decimal NewGoalTarget { get; set; }
    [ObservableProperty] public partial decimal NewGoalCurrent { get; set; }
    [ObservableProperty] public partial DateTime NewGoalDate { get; set; } = DateTime.Today.AddMonths(6);
    [ObservableProperty] public partial bool NewGoalHasDate { get; set; } = true;

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGoalName) || NewGoalTarget <= 0m) return;

        var vm = new SavingsGoalViewModel
        {
            Id = Guid.NewGuid(),
            Name = NewGoalName.Trim(),
            TargetAmount = NewGoalTarget,
            CurrentAmount = NewGoalCurrent,
            TargetDate = NewGoalHasDate ? DateOnly.FromDateTime(NewGoalDate) : null
        };
        SavingsGoals.Add(vm);
        OnPropertyChanged(nameof(HasSavingsGoals));

        NewGoalName = "";
        NewGoalTarget = 0m;
        NewGoalCurrent = 0m;

        RefreshSummary();
        await PersistGoalAsync(vm).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveGoalAsync(SavingsGoalViewModel? item)
    {
        if (item is null) return;
        SavingsGoals.Remove(item);
        OnPropertyChanged(nameof(HasSavingsGoals));
        RefreshSummary();

        try { await _store.RemoveSavingsGoalAsync(item.Id, DateTimeOffset.UtcNow).ConfigureAwait(false); }
        catch { }
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    [ObservableProperty] public partial decimal TotalBudgeted { get; set; }
    [ObservableProperty] public partial decimal TotalSpent { get; set; }
    [ObservableProperty] public partial decimal Unallocated { get; set; }
    [ObservableProperty] public partial decimal Remaining { get; set; }
    [ObservableProperty] public partial decimal TotalRecurring { get; set; }
    [ObservableProperty] public partial decimal TotalSavingsContribution { get; set; }
    [ObservableProperty] public partial bool IsFullyAllocated { get; set; }

    private void RefreshSummary()
    {
        if (MonthlyNetIncome <= 0m || Categories.Count == 0) return;

        var budget = BuildDomainBudget();
        var transactions = BuildDomainTransactions();
        var bills = BuildDomainBills();
        var goals = BuildDomainGoals();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var summary = _calc.Calculate(budget, transactions, today, bills, goals);

        TotalBudgeted = summary.TotalBudgeted;
        TotalSpent = summary.TotalSpent;
        Unallocated = summary.Unallocated;
        Remaining = summary.Remaining;
        TotalRecurring = summary.TotalRecurring;
        TotalSavingsContribution = summary.TotalSavingsContribution;
        IsFullyAllocated = summary.IsFullyAllocated;

        for (var i = 0; i < Categories.Count; i++)
        {
            var vm = Categories[i];
            var cat = summary.Categories.FirstOrDefault(
                c => string.Equals(c.Name, vm.Name, StringComparison.OrdinalIgnoreCase));
            if (cat is null) continue;
            vm.Budgeted = cat.Budgeted;
            vm.Spent = cat.Spent;
            vm.ProjectedMonthEnd = cat.ProjectedMonthEnd;
            vm.Recurring = cat.Recurring;
        }
    }

    private Budget BuildDomainBudget() => new()
    {
        Name = BudgetName,
        MonthlyNetIncome = MonthlyNetIncome,
        Method = Method,
        Categories = Categories.Select(vm => new BudgetCategory
        {
            Name = vm.Name,
            BudgetType = vm.BudgetType,
            Amount = vm.Amount,
            AmountType = vm.AmountType
        }).ToList()
    };

    private IReadOnlyList<BudgetTransaction> BuildDomainTransactions() =>
        Transactions.Select(vm => new BudgetTransaction
        {
            Id = vm.Id,
            CategoryName = vm.CategoryName,
            Amount = vm.Amount,
            Date = vm.Date,
            Description = vm.Description
        }).ToList();

    private IReadOnlyList<RecurringBill> BuildDomainBills() =>
        RecurringBills.Select(vm => new RecurringBill
        {
            Id = vm.Id,
            Name = vm.Name,
            CategoryName = vm.CategoryName,
            Amount = vm.Amount,
            Frequency = vm.Frequency,
            DueDayOfMonth = vm.DueDayOfMonth
        }).ToList();

    private IReadOnlyList<SavingsGoal> BuildDomainGoals() =>
        SavingsGoals.Select(vm => vm.ToDomain()).ToList();

    // ── Reports (C3) ─────────────────────────────────────────────────────────

    public bool IsPro => _entitlements.IsPro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    public partial BudgetReport? Report { get; set; }

    [ObservableProperty] public partial int ReportMonthsBack { get; set; } = 5;

    public bool HasReport => Report is not null;

    [RelayCommand]
    private void GenerateReport()
    {
        if (!_entitlements.IsPro || MonthlyNetIncome <= 0m || Categories.Count == 0) return;
        var months = Math.Clamp(ReportMonthsBack, 1, 12);
        var budget = BuildDomainBudget();
        var allTx  = BuildDomainTransactions();
        Report = _reportCalc.Compute(budget, allTx, DateOnly.FromDateTime(DateTime.Today), months - 1);
    }

    [RelayCommand]
    private async Task ExportReportCsvAsync()
    {
        if (Report is null) return;
        try
        {
            var csv = BudgetReportCsvRenderer.Render(Report);
            await _csvExport.ExportAndOpenAsync(csv, $"budget-report-{DateTime.Today:yyyy-MM}").ConfigureAwait(false);
        }
        catch { }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>Loads persisted budget + transactions on first use.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var budgetSet = await _store.LoadBudgetsAsync().ConfigureAwait(false);
            var txSet     = await _store.LoadTransactionsAsync().ConfigureAwait(false);
            var billSet   = await _store.LoadRecurringBillsAsync().ConfigureAwait(false);
            var goalSet   = await _store.LoadSavingsGoalsAsync().ConfigureAwait(false);

            var dto = budgetSet.Budgets.FirstOrDefault(b =>
                string.Equals(b.Name, BudgetName, StringComparison.OrdinalIgnoreCase));
            if (dto is not null)
            {
                MonthlyNetIncome = dto.MonthlyNetIncome;
                Method = dto.Method;
                foreach (var cat in dto.Categories)
                    Categories.Add(new BudgetCategoryViewModel
                    {
                        Name = cat.Name,
                        BudgetType = cat.BudgetType,
                        Amount = cat.Amount,
                        AmountType = cat.AmountType
                    });
                OnPropertyChanged(nameof(HasCategories));
            }

            foreach (var tx in txSet.Transactions
                         .Where(t => string.Equals(t.BudgetName, BudgetName, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(t => t.Date))
            {
                Transactions.Add(new BudgetTransactionViewModel
                {
                    Id = tx.Id,
                    CategoryName = tx.CategoryName,
                    Amount = tx.Amount,
                    Date = tx.Date,
                    Description = tx.Description
                });
            }
            OnPropertyChanged(nameof(HasTransactions));

            foreach (var bill in billSet.Bills
                         .Where(b => string.Equals(b.BudgetName, BudgetName, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(b => b.Name))
            {
                RecurringBills.Add(new RecurringBillViewModel
                {
                    Id = bill.Id,
                    Name = bill.Name,
                    CategoryName = bill.CategoryName,
                    Amount = bill.Amount,
                    Frequency = bill.Frequency,
                    DueDayOfMonth = bill.DueDayOfMonth
                });
            }
            OnPropertyChanged(nameof(HasRecurringBills));

            foreach (var goal in goalSet.Goals
                         .Where(g => string.Equals(g.BudgetName, BudgetName, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(g => g.Name))
            {
                SavingsGoals.Add(new SavingsGoalViewModel
                {
                    Id = goal.Id,
                    Name = goal.Name,
                    TargetAmount = goal.TargetAmount,
                    CurrentAmount = goal.CurrentAmount,
                    TargetDate = goal.TargetDate
                });
            }
            OnPropertyChanged(nameof(HasSavingsGoals));

            RefreshSummary();
        }
        catch { }
    }

    private void PersistBudget()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var dto = new BudgetDto
                {
                    Name = BudgetName,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    MonthlyNetIncome = MonthlyNetIncome,
                    Method = Method,
                    Categories = Categories.Select(vm => new Shared.Budgeting.BudgetCategoryDto
                    {
                        Name = vm.Name,
                        BudgetType = vm.BudgetType,
                        Amount = vm.Amount,
                        AmountType = vm.AmountType
                    }).ToList()
                };
                await _store.UpsertBudgetAsync(dto).ConfigureAwait(false);
            }
            catch { }
        });
    }

    private async Task PersistTransactionAsync(BudgetTransactionViewModel vm)
    {
        try
        {
            var dto = new TransactionDto
            {
                Id = vm.Id,
                BudgetName = BudgetName,
                CategoryName = vm.CategoryName,
                Amount = vm.Amount,
                Date = vm.Date,
                Description = vm.Description,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await _store.UpsertTransactionAsync(dto).ConfigureAwait(false);
        }
        catch { }
    }

    private async Task PersistBillAsync(RecurringBillViewModel vm)
    {
        try
        {
            var dto = new RecurringBillDto
            {
                Id = vm.Id,
                BudgetName = BudgetName,
                Name = vm.Name,
                CategoryName = vm.CategoryName,
                Amount = vm.Amount,
                Frequency = vm.Frequency,
                DueDayOfMonth = vm.DueDayOfMonth,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await _store.UpsertRecurringBillAsync(dto).ConfigureAwait(false);
        }
        catch { }
    }

    private async Task PersistGoalAsync(SavingsGoalViewModel vm)
    {
        try
        {
            var dto = new SavingsGoalDto
            {
                Id = vm.Id,
                BudgetName = BudgetName,
                Name = vm.Name,
                TargetAmount = vm.TargetAmount,
                CurrentAmount = vm.CurrentAmount,
                TargetDate = vm.TargetDate,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await _store.UpsertSavingsGoalAsync(dto).ConfigureAwait(false);
        }
        catch { }
    }
}
