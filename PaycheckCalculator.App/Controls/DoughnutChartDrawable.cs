using PaycheckCalculator.App.Models;

namespace PaycheckCalculator.App.Controls;

public sealed class DoughnutChartDrawable : IDrawable
{
    private const float ChartWidthRatio = 0.50f;
    private const float ChartHeightRatio = 0.55f;
    private const float InnerRadiusRatio = 0.55f;
    private const int MinArcSegments = 8;
    private const float DegreesPerSegment = 3f;

    public ResultCardModel? Result { get; set; }

    // Mirrors the token ramp in Resources/Styles/Colors.xaml. Kept as literals because
    // IDrawable has no access to the XAML resource dictionary at draw time.
    private static readonly Color[] SliceColors =
    {
        Color.FromArgb("#EF4444"), // Federal Tax        — Danger
        Color.FromArgb("#94A3B8"), // Social Security    — Chart3
        Color.FromArgb("#CBD5E1"), // Medicare Tax       — BorderStrong
        Color.FromArgb("#60A5FA"), // State Income Tax   — Chart2
        Color.FromArgb("#93C5FD"), // State Disability   — Chart2 (light)
        Color.FromArgb("#475569"), // Pre-Tax Deductions — Chart4
        Color.FromArgb("#64748B"), // Post-Tax Deductions— Muted
        Color.FromArgb("#2563EB"), // Net Pay            — Primary
    };

    private static readonly Color InkColor = Color.FromArgb("#0F172A");
    private static readonly Color MutedColor = Color.FromArgb("#64748B");
    private static readonly Color BodyColor = Color.FromArgb("#334155");

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

        // Centre label: share of gross pay that survives as take-home.
        float takeHomePct = (float)Result.NetPay / gross * 100f;

        canvas.FontColor = InkColor;
        canvas.FontSize = innerRadius * 0.44f;
        canvas.DrawString($"{takeHomePct:F1}%",
            centerX - innerRadius, centerY - innerRadius * 0.55f,
            innerRadius * 2f, innerRadius * 0.8f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.FontColor = MutedColor;
        canvas.FontSize = 11f;
        canvas.DrawString("take-home",
            centerX - innerRadius, centerY + innerRadius * 0.18f,
            innerRadius * 2f, 16f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        // Legend rows below the chart.
        float legendY = centerY + outerRadius + 22f;
        float legendX = 16f;
        float lineHeight = 24f;
        float swatchSize = 10f;
        float rowWidth = dirtyRect.Width - legendX * 2f;

        foreach (var (name, value, color) in slices)
        {
            float pct = value / gross * 100f;

            canvas.FillColor = color;
            canvas.FillRoundedRectangle(legendX, legendY + 5f, swatchSize, swatchSize, swatchSize / 2f);

            canvas.FontSize = 13f;
            canvas.FontColor = BodyColor;
            canvas.DrawString(name,
                legendX + swatchSize + 10f, legendY,
                rowWidth - swatchSize - 90f, lineHeight,
                HorizontalAlignment.Left, VerticalAlignment.Center);

            canvas.FontColor = InkColor;
            canvas.DrawString($"{pct:F1}%",
                legendX + rowWidth - 70f, legendY,
                70f, lineHeight,
                HorizontalAlignment.Right, VerticalAlignment.Center);

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
