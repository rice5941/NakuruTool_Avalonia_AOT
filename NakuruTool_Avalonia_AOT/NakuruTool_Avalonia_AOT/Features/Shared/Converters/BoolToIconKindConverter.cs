using Avalonia.Data.Converters;
using Material.Icons;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// Boolean値に基づいてMaterialIconKindを返すコンバーター
    /// </summary>
    public class BoolToIconKindConverter : IValueConverter
    {
        public MaterialIconKind TrueIcon { get; set; } = MaterialIconKind.CheckboxMarked;
        public MaterialIconKind FalseIcon { get; set; } = MaterialIconKind.CheckboxBlankOutline;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueIcon : FalseIcon;
            }

            return FalseIcon;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MaterialIconKind iconKind)
            {
                return iconKind == TrueIcon;
            }

            return false;
        }
    }
}
