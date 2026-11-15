using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// Prism の6ジャンルの表示（譜面一覧の1行に描く）。
/// <list type="bullet">
/// <item>ジャンル濃度モード: 左に六角形のレーダーチャート（外周が <see cref="RadarScale"/>%）、右に凡例（ジャンル名・濃度）。</item>
/// <item>基準スキル値モード: 6ジャンルと LN の7本の横棒と数値。満幅は <see cref="SkillBarMaximum"/>。</item>
/// </list>
/// 行ごとに TextBlock を並べないよう、まとめて描画する。
/// </summary>
public sealed class PrismGenreChart : Control
{
    public static readonly StyledProperty<ManiaChartAnalysis?> AnalysisProperty =
        AvaloniaProperty.Register<PrismGenreChart, ManiaChartAnalysis?>(nameof(Analysis));

    public static readonly StyledProperty<bool> ShowSkillValuesProperty =
        AvaloniaProperty.Register<PrismGenreChart, bool>(nameof(ShowSkillValues));

    /// <summary>レーダーチャートの外周にあたるジャンル濃度（%）。超えた分は外周で止める。</summary>
    public const double RadarScale = 50;

    /// <summary>
    /// ジャンルの色（<see cref="PrismGenre"/> の順）。Chordstream〜Jack は huuugeosu の軸の色、
    /// ChordJack は Jack と並べて見分けやすい黄色。
    /// </summary>
    private static readonly IBrush[] s_genreBrushes =
    [
        new ImmutableSolidColorBrush(Color.Parse("#6f87d8")),
        new ImmutableSolidColorBrush(Color.Parse("#b06bc0")),
        new ImmutableSolidColorBrush(Color.Parse("#5fb8c9")),
        new ImmutableSolidColorBrush(Color.Parse("#83a86f")),
        new ImmutableSolidColorBrush(Color.Parse("#c59a5c")),
        new ImmutableSolidColorBrush(Color.Parse("#d4c04a")),
    ];

    /// <summary>LN の色（huuugeosu の LN 軸の色）</summary>
    private static readonly IBrush s_lnBrush = new ImmutableSolidColorBrush(Color.Parse("#c66f84"));

    private static readonly Color s_shapeColor = Color.Parse("#4f8ef7");
    private static readonly IBrush s_shapeFill = new ImmutableSolidColorBrush(s_shapeColor, 0.3);
    private static readonly IPen s_shapePen = new ImmutablePen(new ImmutableSolidColorBrush(s_shapeColor), 1.5);
    private static readonly IPen s_gridPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Gray, 0.35), 1);
    private static readonly IBrush s_trackBrush = new ImmutableSolidColorBrush(Colors.Gray, 0.18);

    // 濃度モード
    private const double MaxRadarDiameter = 80;
    private const double RadarGap = 10;
    private const double DotSize = 7;
    // 基準スキル値モード
    private const double LabelWidth = 64;
    private const double ValueWidth = 28;

    private const double FontSize = 10.5;
    private static readonly Typeface s_typeface = new(FontFamily.Default);

    static PrismGenreChart()
    {
        AffectsRender<PrismGenreChart>(AnalysisProperty, ShowSkillValuesProperty, TextElement.ForegroundProperty);
    }

    public ManiaChartAnalysis? Analysis
    {
        get => GetValue(AnalysisProperty);
        set => SetValue(AnalysisProperty, value);
    }

    /// <summary>true なら基準スキル値の横棒、false ならジャンル濃度のレーダーチャートを描く</summary>
    public bool ShowSkillValues
    {
        get => GetValue(ShowSkillValuesProperty);
        set => SetValue(ShowSkillValuesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var analysis = Analysis;
        if (analysis is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var foreground = TextElement.GetForeground(this) ?? Brushes.Gray;
        if (ShowSkillValues)
            RenderSkillBars(context, analysis, foreground);
        else
            RenderConcentration(context, analysis, foreground);
    }

    // ───────────────────────────────────────────
    // ジャンル濃度: 六角形と凡例
    // ───────────────────────────────────────────

    private void RenderConcentration(DrawingContext context, ManiaChartAnalysis analysis, IBrush foreground)
    {
        var genres = GenreValues.Genres;
        double radius = Math.Min(MaxRadarDiameter, Bounds.Height - 4) / 2;
        var center = new Point(radius + 1, Bounds.Height / 2);
        if (radius > 0)
            DrawRadar(context, center, radius, analysis.GenreConcentrations);

        // 凡例: ● ジャンル名 … 濃度（表示の合計が 100 になるよう丸めた値）
        var dim = foreground is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, 0.75) : foreground;
        var shares = analysis.GenreTendencies.ToRoundedShares();
        double legendLeft = center.X + radius + RadarGap;
        double rowHeight = Bounds.Height / genres.Length;
        double fontSize = FontSizeFor(rowHeight);
        for (int i = 0; i < genres.Length; i++)
        {
            double centerY = i * rowHeight + rowHeight / 2;
            context.DrawEllipse(s_genreBrushes[i], null, new Point(legendLeft + DotSize / 2, centerY), DotSize / 2, DotSize / 2);

            var label = Text(genres[i].ToString(), foreground, fontSize);
            context.DrawText(label, new Point(legendLeft + DotSize + 4, centerY - label.Height / 2));

            var share = Text(shares[i].ToString(CultureInfo.InvariantCulture) + "%", dim, fontSize);
            context.DrawText(share, new Point(Bounds.Width - share.Width, centerY - share.Height / 2));
        }
    }

    /// <summary>
    /// 頂点は 12 時から時計回りに表示順（Chordstream → Bracket → Delay → Technical → Jack → ChordJack）。
    /// 目盛りは外周（<see cref="RadarScale"/>%）と半分の2重。
    /// </summary>
    private static void DrawRadar(DrawingContext context, Point center, double radius, GenreValues shares)
    {
        var genres = GenreValues.Genres;

        context.DrawGeometry(null, s_gridPen, Polygon(center, radius, _ => 1));
        context.DrawGeometry(null, s_gridPen, Polygon(center, radius, _ => 0.5));
        for (int i = 0; i < genres.Length; i++)
            context.DrawLine(s_gridPen, center, Vertex(center, radius, i, 1));

        double Ratio(int i) => Math.Clamp(shares[genres[i]] / RadarScale, 0, 1);
        if (shares.Sum() > 0)
            context.DrawGeometry(s_shapeFill, s_shapePen, Polygon(center, radius, Ratio));

        for (int i = 0; i < genres.Length; i++)
        {
            if (Ratio(i) > 0)
                context.DrawEllipse(s_genreBrushes[i], null, Vertex(center, radius, i, Ratio(i)), 2.5, 2.5);
        }
    }

    private static Point Vertex(Point center, double radius, int index, double ratio)
    {
        double radians = (-90 + index * 360.0 / GenreValues.Genres.Length) * Math.PI / 180;
        return new Point(center.X + radius * ratio * Math.Cos(radians), center.Y + radius * ratio * Math.Sin(radians));
    }

    private static StreamGeometry Polygon(Point center, double radius, Func<int, double> ratio)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Vertex(center, radius, 0, ratio(0)), true);
            for (int i = 1; i < GenreValues.Genres.Length; i++)
                ctx.LineTo(Vertex(center, radius, i, ratio(i)));
            ctx.EndFigure(true);
        }
        return geometry;
    }

    // ───────────────────────────────────────────
    // 基準スキル値: 横棒
    // ───────────────────────────────────────────

    /// <summary>
    /// 横棒の満幅: Reg 基準Overall・LN 基準Overall・6ジャンルの基準スキル値のうち最大の値。
    /// LN 譜面では LN が Reg 基準Overall を超えることがあるため、7本を同じ物差しで比べられるようにする。
    /// </summary>
    public static double SkillBarMaximum(ManiaChartAnalysis analysis)
    {
        double maximum = Math.Max(analysis.PrismOverall, analysis.PrismLnOverall ?? 0);
        foreach (var genre in GenreValues.Genres)
            maximum = Math.Max(maximum, analysis.GenreSkills[genre]);
        return maximum;
    }

    private void RenderSkillBars(DrawingContext context, ManiaChartAnalysis analysis, IBrush foreground)
    {
        if (Bounds.Width <= LabelWidth + ValueWidth)
            return;

        var genres = GenreValues.Genres;
        int rows = genres.Length + 1;
        double maximum = SkillBarMaximum(analysis);
        double rowHeight = Bounds.Height / rows;
        double fontSize = FontSizeFor(rowHeight);
        double barWidth = Bounds.Width - LabelWidth - ValueWidth - 4;
        double barHeight = Math.Clamp(rowHeight * 0.45, 3, 8);

        for (int i = 0; i < rows; i++)
        {
            // 6ジャンルの後に LN（測定不能なら棒なし・数値は "-"）
            bool isLn = i == genres.Length;
            string name = isLn ? ManiaChartAnalysis.LnAxis : genres[i].ToString();
            double? value = isLn ? analysis.PrismLnOverall : analysis.GenreSkills[genres[i]];
            var brush = isLn ? s_lnBrush : s_genreBrushes[i];
            double centerY = i * rowHeight + rowHeight / 2;

            var label = Text(name, foreground, fontSize);
            context.DrawText(label, new Point(0, centerY - label.Height / 2));

            var barRect = new Rect(LabelWidth, centerY - barHeight / 2, barWidth, barHeight);
            context.DrawRectangle(s_trackBrush, null, barRect, barHeight / 2, barHeight / 2);

            double ratio = value is { } v && maximum > 0 ? Math.Clamp(v / maximum, 0, 1) : 0;
            if (ratio > 0)
            {
                var fill = barRect.WithWidth(Math.Max(barHeight, barWidth * ratio));
                context.DrawRectangle(brush, null, fill, barHeight / 2, barHeight / 2);
            }

            var number = Text(value is { } shown ? shown.ToString("F1", CultureInfo.InvariantCulture) : "-", foreground, fontSize);
            context.DrawText(number, new Point(Bounds.Width - number.Width, centerY - number.Height / 2));
        }
    }

    /// <summary>行が狭いとき（7本の横棒など）は、文字が隣の行に重ならないよう少し小さくする</summary>
    private static double FontSizeFor(double rowHeight) => Math.Clamp(rowHeight * 0.8, 8, FontSize);

    private static FormattedText Text(string text, IBrush brush, double fontSize)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, s_typeface, fontSize, brush);
}
