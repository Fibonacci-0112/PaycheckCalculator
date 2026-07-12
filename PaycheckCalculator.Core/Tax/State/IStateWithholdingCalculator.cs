using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// State tax calculator interface. Each implementation handles a single state's
/// withholding rules. The UI input schema is no longer declared here — schemas
/// live as JSON under <c>PaycheckCalculator.Core/Data/Schemas/&lt;state&gt;.json</c>
/// and are consumed via <see cref="IStateSchemaProvider"/>.
/// </summary>
public interface IStateWithholdingCalculator
{
    /// <summary>The state this calculator handles.</summary>
    UsState State { get; }

    /// <summary>
    /// Validates the user-supplied values against this state's rules.
    /// Returns an empty list when all values are valid.
    /// </summary>
    IReadOnlyList<string> Validate(StateInputValues values);

    /// <summary>
    /// Calculates state income tax withholding for one pay period.
    /// </summary>
    StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values);
}
