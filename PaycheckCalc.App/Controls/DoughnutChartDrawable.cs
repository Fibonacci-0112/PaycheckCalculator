using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Controls;

public sealed class DoughnutChartDrawable : IDrawable
{
    private const float ChartWidthRatio = 0.50f;
    private const float ChartHeightRatio = 0.55f;
    private const float InnerRadiusRatio = 0.55f;
    private const int MinArcSegments = 8;
    private const float DegreesPerSegment = 3f;

    public ResultCardModel? Result { get; set; }

    private static readonly Color[] SliceColors =
    {
        Color.FromArgb("#C62828"), // Federal Tax - red
        Color.FromArgb("#1565C0"), // Social Security Tax - blue
        Color.FromArgb("#6A1B9A"), // Medicare Tax - purple
        Color.FromArgb("#EF6C00"), // State Income Tax - orange
        Color.FromArgb("#F9A825"), // State Disability Insurance - amber
        Color.FromArgb("#5D4037"), // Pre-Tax Deductions - brown
        Color.FromArgb("#795548"), // Post-Tax Deductions - light brown
        Color.FromArgb("#2E7D32"), // Net Pay - green
    };

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Result is null || Result.GrossPay <= 0) return;

        var gross = (float)Result.GrossPay;

        var slices = new List<(string Name, float Value, Color Color)>();

        if (Result.FederalWithholding > 0)
            slices.Add(("Federal Tax", (float)Result.FederalWithholding, SliceColors[0]));
        if (Result.SocialSecurityWithholding > 0)
            slices.Add(("Social Security", (float)Result.SocialSecurityWithholding, SliceColors[1]));
        if (Result.MedicareWithholding + Result.AdditionalMedicareWithholding > 0)
            slices.Add(("Medicare Tax", (float)(Result.MedicareWithholding + Result.AdditionalMedicareWithholding), SliceColors[2]));
        if (Result.StateWithholding > 0)
            slices.Add(("State Income Tax", (float)Result.StateWithholding, SliceColors[3]));
        if (Result.StateDisabilityInsurance > 0)
            slices.Add((Result.StateDisabilityInsuranceLabel, (float)Result.StateDisabilityInsurance, SliceColors[4]));
        if (Result.PreTaxDeductions > 0)
            slices.Add(("Pre-Tax Deductions", (float)Result.PreTaxDeductions, SliceColors[5]));
        if (Result.PostTaxDeductions > 0)
            slices.Add(("Post-Tax Deductions", (float)Result.PostTaxDeductions, SliceColors[6]));
        if (Result.NetPay > 0)
            slices.Add(("Net Pay", (float)Result.NetPay, SliceColors[7]));

        if (slices.Count == 0) return;

        // Layout constants
        float chartDiameter = Math.Min(dirtyRect.Width * ChartWidthRatio, dirtyRect.Height * ChartHeightRatio);
        float outerRadius = chartDiameter / 2f;
        float innerRadius = outerRadius * InnerRadiusRatio;
        float centerX = dirtyRect.Width / 2f;
        float centerY = outerRadius + 10f;

        // Draw slices
        float startAngle = -90f;
        foreach (var (name, value, color) in slices)
        {
            float sweepAngle = (value / gross) * 360f;
            DrawSlice(canvas, centerX, centerY, outerRadius, innerRadius, startAngle, sweepAngle, color);
            startAngle += sweepAngle;
        }

        // Draw legend below chart
        float legendY = centerY + outerRadius + 20f;
        float legendX = 16f;
        float lineHeight = 22f;
        float swatchSize = 12f;

        canvas.FontSize = 13f;

        foreach (var (name, value, color) in slices)
        {
            float pct = value / gross * 100f;
            string label = $"{name}  {pct:F2}%";

            canvas.FillColor = color;
            canvas.FillRoundedRectangle(legendX, legendY, swatchSize, swatchSize, 2f);

            canvas.FontColor = Color.FromArgb("#37474F");
            canvas.DrawString(label, legendX + swatchSize + 8f, legendY, dirtyRect.Width - legendX - swatchSize - 24f, lineHeight, HorizontalAlignment.Left, VerticalAlignment.Top);

            legendY += lineHeight;
        }
    }

    private static void DrawSlice(ICanvas canvas, float cx, float cy, float outerR, float innerR, float startAngle, float sweepAngle, Color color)
    {
        if (sweepAngle < 0.1f) return;

        var path = new PathF();

        // Outer arc start point
        float startRad = startAngle * MathF.PI / 180f;
        float endRad = (startAngle + sweepAngle) * MathF.PI / 180f;

        // Build the slice as: outer arc → line to inner arc → inner arc (reverse) → close
        float outerStartX = cx + outerR * MathF.Cos(startRad);
        float outerStartY = cy + outerR * MathF.Sin(startRad);

        path.MoveTo(outerStartX, outerStartY);

        // Approximate arcs with line segments
        int segments = Math.Max(MinArcSegments, (int)(sweepAngle / DegreesPerSegment));
        float angleStep = sweepAngle / segments;

        // Outer arc
        for (int i = 1; i <= segments; i++)
        {
            float angle = (startAngle + angleStep * i) * MathF.PI / 180f;
            path.LineTo(cx + outerR * MathF.Cos(angle), cy + outerR * MathF.Sin(angle));
        }

        // Line to inner arc end point
        float innerEndX = cx + innerR * MathF.Cos(endRad);
        float innerEndY = cy + innerR * MathF.Sin(endRad);
        path.LineTo(innerEndX, innerEndY);

        // Inner arc (reverse direction)
        for (int i = segments - 1; i >= 0; i--)
        {
            float angle = (startAngle + angleStep * i) * MathF.PI / 180f;
            path.LineTo(cx + innerR * MathF.Cos(angle), cy + innerR * MathF.Sin(angle));
        }

        path.Close();

        canvas.FillColor = color;
        canvas.FillPath(path);
    }
}
