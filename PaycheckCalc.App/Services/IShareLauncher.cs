namespace PaycheckCalc.App.Services;

/// <summary>
/// Presents a file to the user through the platform share sheet. Abstracts the
/// MAUI <c>Share</c> Essentials API so services that produce files (e.g. CSV
/// export) stay decoupled from the static platform call and remain testable.
/// </summary>
public interface IShareLauncher
{
    Task ShareAsync(string filePath, string title);
}
