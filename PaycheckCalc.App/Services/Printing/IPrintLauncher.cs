namespace PaycheckCalc.App.Services.Printing;

/// <summary>
/// Hands an already-rendered PDF file to the platform print system. Abstracts
/// the platform-specific printing call so <see cref="PrintService"/> stays free
/// of <c>#if</c> platform code.
/// </summary>
public interface IPrintLauncher
{
    Task PrintAsync(string filePath, string jobName);
}
