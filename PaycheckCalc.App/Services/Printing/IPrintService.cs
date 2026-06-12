using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Printing;

/// <summary>
/// Sends the current per-period paycheck results to the platform print system.
/// </summary>
public interface IPrintService
{
    /// <summary>
    /// Renders the results to a single-page document and hands it to the
    /// platform print framework (the Android print dialog or the Windows default
    /// print handler).
    /// </summary>
    Task PrintAsync(ResultCardModel result);
}
