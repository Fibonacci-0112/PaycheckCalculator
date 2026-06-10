using Microsoft.Maui.Graphics;
using PaycheckCalc.App.Controls;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <summary>
/// Renders the same <see cref="DoughnutChartDrawable"/> the Results page draws
/// into an off-screen bitmap and encodes it as a baseline JPEG suitable for
/// embedding in the PDF.
/// </summary>
internal static class ChartImageRenderer
{
    /// <summary>
    /// Draws the chart for <paramref name="result"/> at the given pixel size.
    /// Returns <c>null</c> when there is nothing to chart (no gross pay).
    /// </summary>
    public static (byte[] Jpeg, int Width, int Height)? Render(ResultCardModel result, int width, int height)
    {
        if (result.GrossPay <= 0)
            return null;

        var drawable = new DoughnutChartDrawable { Result = result };

        using var context = GraphicsPlatform.CurrentService.CreateBitmapExportContext(width, height);
        var canvas = context.Canvas;

        // JPEG has no alpha; paint the card's white background first so any
        // transparent pixels don't encode as black.
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(0, 0, width, height);

        drawable.Draw(canvas, new RectF(0, 0, width, height));

        using var stream = new MemoryStream();
        context.Image.Save(stream, ImageFormat.Jpeg, 0.9f);
        return (stream.ToArray(), width, height);
    }
}
