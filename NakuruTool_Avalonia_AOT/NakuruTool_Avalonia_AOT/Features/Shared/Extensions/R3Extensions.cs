using R3;
using System;
using System.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Extensions;

/// <summary>
/// R3関連の拡張メソッド
/// NativeAOT対応: string.Emptyの代わりにnameofを使用
/// </summary>
public static class R3Extensions
{
    /// <summary>
    /// INotifyPropertyChangedの特定プロパティの変更を監視するObservableを作成
    /// </summary>
    /// <typeparam name="T">INotifyPropertyChangedを実装する型</typeparam>
    /// <param name="source">監視対象のオブジェクト</param>
    /// <param name="propertyName">監視するプロパティ名</param>
    /// <returns>プロパティ変更を通知するObservable</returns>
    public static Observable<PropertyChangedEventArgs> ObserveProperty<T>(
        this T source,
        string propertyName) where T : INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            static h => (sender, e) => h(e),
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h)
            .Where(e => e.PropertyName == propertyName);
    }

    /// <summary>
    /// INotifyPropertyChangedの特定プロパティの変更を監視し、アクションを実行
    /// </summary>
    /// <typeparam name="T">INotifyPropertyChangedを実装する型</typeparam>
    /// <param name="source">監視対象のオブジェクト</param>
    /// <param name="propertyName">監視するプロパティ名</param>
    /// <param name="action">プロパティ変更時に実行するアクション</param>
    /// <param name="disposables">購読を追加するCompositeDisposable</param>
    public static void ObservePropertyAndSubscribe<T>(
        this T source,
        string propertyName,
        Action action,
        CompositeDisposable disposables) where T : INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(disposables);

        source.ObserveProperty(propertyName)
            .Subscribe(_ => action())
            .AddTo(disposables);
    }
}
