using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Central registry that maps each <see cref="UsState"/> to its
/// <see cref="IStateWithholdingCalculator"/>. Provides lookup by state
/// and exposes the full list of supported states for UI picker population.
/// </summary>
public sealed class StateCalculatorRegistry
{
    private readonly Dictionary<UsState, IStateWithholdingCalculator> _calculators = new();
    private IReadOnlyList<UsState>? _sortedStates;

    /// <summary>Register a calculator. Replaces any existing registration for the state.</summary>
    public void Register(IStateWithholdingCalculator calculator)
    {
        _calculators[calculator.State] = calculator;
        _sortedStates = null; // invalidate cache
    }

    /// <summary>Returns true when a calculator has been registered for <paramref name="state"/>.</summary>
    public bool IsSupported(UsState state) => _calculators.ContainsKey(state);

    /// <summary>
    /// Gets the calculator for the given state.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when no calculator is registered for <paramref name="state"/>.</exception>
    public IStateWithholdingCalculator GetCalculator(UsState state)
    {
        if (_calculators.TryGetValue(state, out var calc))
            return calc;

        throw new NotSupportedException(
            $"State withholding calculator for {state} has not been registered.");
    }

    /// <summary>Returns all states that have a registered calculator, sorted alphabetically.</summary>
    public IReadOnlyList<UsState> SupportedStates =>
        _sortedStates ??= _calculators.Keys.OrderBy(s => s.ToString()).ToList();
}
