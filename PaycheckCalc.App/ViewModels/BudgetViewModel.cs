using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.Core.Budgeting;
using PaycheckCalc.Shared.Budgeting;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

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
    private bool _initialized;

    public BudgetViewModel(BudgetCalculator calc, IBudgetStore store, CalculatorViewModel calculator)
    {
        _calc = calc;
        _store = store;
        _calculator = calculator;
    }

    // ── Identity ─────────────────────────────────────────────────────────────

    [ObservableProperty] public partial string BudgetName { get; set; } = "My Budget";

    // ── Monthly income ────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIncome))]
    public partial decimal MonthlyNetIncome { get; set; }

    public bool HasIncome => MonthlyNetIncome > 0m;

    // ── Categories ────────────────────────────────────────────────────────────

    public ObservableCollection<BudgetCategoryViewModel> Categories { get; } = new();

    public bool HasCategories => Categories.Count > 0;

    partial void OnMonthlyNetIncomeChanged(decimal value) => RefreshSummary();

    /// <summary>
    /// Applies the 50/30/20 allocation rule using the most recently calculated paycheck's net pay
    /// and frequency as the monthly income basis.
    /// </summary>
    [RelayCommand]
    private void Apply5030_20()
    {
        var resultCard = _calculator.ResultCard;
        if (resultCard is null) return;

        var monthly = MonthlyIncomeNormalizer.ToMonthly(resultCard.NetPay, _calculator.Frequency);
        MonthlyNetIncome = monthly;

        var preset = AllocationRules.FiftyThirtyTwenty();
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

        OnPropertyChanged(nameof(HasCategories));
        RefreshSummary();
        PersistBudget();
    }

    [RelayCommand]
    private void AddCategory()
    {
        Categories.Add(new BudgetCategoryViewModel { Name = "New Category", Amount = 0m });
        OnPropertyChanged(nameof(HasCategories));
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

    // ── Summary ───────────────────────────────────────────────────────────────

    [ObservableProperty] public partial decimal TotalBudgeted { get; set; }
    [ObservableProperty] public partial decimal TotalSpent { get; set; }
    [ObservableProperty] public partial decimal Unallocated { get; set; }
    [ObservableProperty] public partial decimal Remaining { get; set; }

    private void RefreshSummary()
    {
        if (MonthlyNetIncome <= 0m || Categories.Count == 0) return;

        var budget = BuildDomainBudget();
        var transactions = BuildDomainTransactions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var summary = _calc.Calculate(budget, transactions, today);

        TotalBudgeted = summary.TotalBudgeted;
        TotalSpent = summary.TotalSpent;
        Unallocated = summary.Unallocated;
        Remaining = summary.Remaining;

        for (var i = 0; i < Categories.Count; i++)
        {
            var vm = Categories[i];
            var cat = summary.Categories.FirstOrDefault(
                c => string.Equals(c.Name, vm.Name, StringComparison.OrdinalIgnoreCase));
            if (cat is null) continue;
            vm.Budgeted = cat.Budgeted;
            vm.Spent = cat.Spent;
            vm.ProjectedMonthEnd = cat.ProjectedMonthEnd;
        }
    }

    private Budget BuildDomainBudget() => new()
    {
        Name = BudgetName,
        MonthlyNetIncome = MonthlyNetIncome,
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

            var dto = budgetSet.Budgets.FirstOrDefault(b =>
                string.Equals(b.Name, BudgetName, StringComparison.OrdinalIgnoreCase));
            if (dto is not null)
            {
                MonthlyNetIncome = dto.MonthlyNetIncome;
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
}
