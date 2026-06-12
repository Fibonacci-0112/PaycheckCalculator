using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace PaycheckCalc.App.Services;

/// <inheritdoc />
public sealed class ShareLauncher : IShareLauncher
{
    /// <inheritdoc />
    public Task ShareAsync(string filePath, string title) =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath),
        });
}
