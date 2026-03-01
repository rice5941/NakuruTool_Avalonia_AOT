using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// RangeBoundaryType と CheckBox の IsChecked をバインドするコンバータ。
/// Inclusive → true（境界値を含む）、Exclusive → false（境界値を含まない）
/// </summary>
public class BoundaryTypeInclusiveToBoolConverter : IValueConverter
{
    public static readonly BoundaryTypeInclusiveToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is RangeBoundaryType boundaryType && boundaryType == RangeBoundaryType.Inclusive;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked)
            return isChecked ? RangeBoundaryType.Inclusive : RangeBoundaryType.Exclusive;

        return RangeBoundaryType.Inclusive;
    }
}
