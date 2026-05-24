using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;
/// <summary>
/// グレード文字列に基づいて色を返すコンバーター
/// S: オレンジ, A: 緑, B: 青, C: ピンク, D: 銅
/// </summary>
public class GradeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush SGradeBrush = new(Color.FromRgb(255, 140, 0));     // オレンジ (DarkOrange)
    private static readonly SolidColorBrush AGradeBrush = new(Color.FromRgb(34, 139, 34));     // 緑 (ForestGreen)
    private static readonly SolidColorBrush BGradeBrush = new(Color.FromRgb(30, 144, 255));    // 青 (DodgerBlue)
    private static readonly SolidColorBrush CGradeBrush = new(Color.FromRgb(219, 112, 147));   // ピンク (PaleVioletRed)
    private static readonly SolidColorBrush DGradeBrush = new(Color.FromRgb(205, 127, 50));    // 銅 (Peru)
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(128, 128, 128));  // グレー

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string grade && !string.IsNullOrEmpty(grade) && grade != "-")
        {
            // SS+, SS, S+, S を S として扱う
            if (grade.Contains("S", StringComparison.OrdinalIgnoreCase))
            {
                return SGradeBrush;
            }
            
            // グレードの最初の文字で判定
            var firstChar = grade[0];
            return firstChar switch
            {
                'A' or 'a' => AGradeBrush,
                'B' or 'b' => BGradeBrush,
                'C' or 'c' => CGradeBrush,
                'D' or 'd' => DGradeBrush,
                _ => DefaultBrush
            };
        }

        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
