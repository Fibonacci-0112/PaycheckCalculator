namespace PaycheckCalc.Core.Budgeting;

public enum BudgetType { Needs, Wants, Savings }
public enum BudgetAmountType { Dollar, Percentage }

/// <summary>
/// The budgeting methodology a <see cref="Budget"/> follows. This is descriptive metadata that drives
/// UI guidance (e.g. zero-based budgets aim for zero unallocated income); it does not change how
/// <see cref="BudgetCategory.EffectiveMonthlyBudget"/> is computed.
/// </summary>
public enum BudgetMethod { Custom, FiftyThirtyTwenty, ZeroBased, Envelope }

/// <summary>How often a <see cref="RecurringBill"/> recurs. Drives the monthly-equivalent conversion.</summary>
public enum RecurrenceFrequency { Weekly, Biweekly, Semimonthly, Monthly, Quarterly, Semiannual, Annual }
