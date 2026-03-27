using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// BeatmapDownloadState を IsVisible (bool) に変換するコンバーター。
    /// ConverterParameter: "Exists", "NotExists", "Queued", "Downloading", "Downloaded", "Error"
    /// </summary>
    public class DownloadStateVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BeatmapDownloadState state &&
                parameter is string stateName &&
                Enum.TryParse<BeatmapDownloadState>(stateName, out var targetState))
            {
                return state == targetState;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// DownloadState と CanDownload の2値から表示判定を行うMultiValueConverter。
    /// values[0]: BeatmapDownloadState, values[1]: bool (CanDownload)
    /// ConverterParameter:
    ///   "NotExistsCanDownload" — NotExists かつ CanDownload
    ///   "NotExistsNoDownload"  — NotExists かつ !CanDownload
    /// </summary>
    public class DownloadStateCanDownloadConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 ||
                values[0] is not BeatmapDownloadState state ||
                values[1] is not bool canDownload ||
                parameter is not string paramStr)
            {
                return false;
            }

            return paramStr switch
            {
                "NotExistsCanDownload" => state == BeatmapDownloadState.NotExists && canDownload,
                "NotExistsNoDownload" => state == BeatmapDownloadState.NotExists && !canDownload,
                _ => false
            };
        }
    }
}
