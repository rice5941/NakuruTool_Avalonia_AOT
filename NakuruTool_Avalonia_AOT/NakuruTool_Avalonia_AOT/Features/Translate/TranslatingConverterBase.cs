using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace NakuruTool_Avalonia_AOT.Features.Translate;

/// <summary>
/// 値を翻訳文字列に変換するコンバータの基底クラス。
/// 通常の Binding では一度だけ評価されるため言語切替に追従しない。
/// MultiBinding の 2 番目に <see cref="LanguageService.CurrentLanguage"/> をバインドすると、
/// 言語変更時に再評価されて表示が切り替わる。
/// </summary>
/// <example>
/// <code>
/// &lt;MultiBinding Converter="{x:Static converters:FilterTargetToStringConverter.Instance}"&gt;
///     &lt;Binding/&gt;
///     &lt;Binding Path="CurrentLanguage" Source="{x:Static translate:LanguageService.Instance}"/&gt;
/// &lt;/MultiBinding&gt;
/// </code>
/// </example>
public abstract class TranslatingConverterBase : IValueConverter, IMultiValueConverter
{
    /// <summary>
    /// 値を現在の言語の文字列に変換する
    /// </summary>
    protected abstract string Translate(object? value);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Translate(value);
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] が変換対象、values[1] 以降は再評価のトリガー（CurrentLanguage）なので値は使わない
        var value = values.Count > 0 ? values[0] : null;
        return Translate(value == AvaloniaProperty.UnsetValue ? null : value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
