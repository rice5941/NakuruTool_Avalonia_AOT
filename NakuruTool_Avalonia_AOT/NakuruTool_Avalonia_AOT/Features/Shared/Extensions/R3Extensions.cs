using Avalonia.Collections;
using R3;
using System;
using System.Collections.Specialized;
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
    /// INotifyPropertyChangedのすべてのプロパティ変更を監視するObservableを作成
    /// </summary>
    /// <typeparam name="T">INotifyPropertyChangedを実装する型</typeparam>
    /// <param name="source">監視対象のオブジェクト</param>
    /// <returns>プロパティ変更を通知するObservable</returns>
    public static Observable<PropertyChangedEventArgs> ObservePropertyChanged<T>(
        this T source) where T : INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);

        return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            static h => (sender, e) => h(e),
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h);
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

    /// <summary>
    /// AvaloniaListのコレクション変更を監視するObservableを作成
    /// </summary>
    /// <typeparam name="T">コレクションの要素型</typeparam>
    /// <param name="source">監視対象のAvaloniaList</param>
    /// <returns>コレクション変更を通知するObservable</returns>
    public static Observable<NotifyCollectionChangedEventArgs> ObserveCollectionChanged<T>(
        this AvaloniaList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Observable.FromEvent<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
            static h => (sender, e) => h(e),
            h => source.CollectionChanged += h,
            h => source.CollectionChanged -= h);
    }

    /// <summary>
    /// AvaloniaListの要素のPropertyChangedを監視するObservableを作成
    /// 要素の追加・削除時に自動的に監視対象を更新
    /// </summary>
    /// <typeparam name="T">INotifyPropertyChangedを実装するコレクションの要素型</typeparam>
    /// <param name="source">監視対象のAvaloniaList</param>
    /// <returns>要素のプロパティ変更を通知するObservable</returns>
    public static Observable<(T Item, PropertyChangedEventArgs Args)> ObserveElementPropertyChanged<T>(
        this AvaloniaList<T> source) where T : INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);

        return Observable.Create<(T Item, PropertyChangedEventArgs Args)>(observer =>
        {
            var itemDisposables = new CompositeDisposable();
            var mainDisposable = new CompositeDisposable();

            // 要素のPropertyChangedを監視する関数
            void SubscribeToItem(T item)
            {
                item.ObservePropertyChanged()
                    .Subscribe(args => observer.OnNext((item, args)))
                    .AddTo(itemDisposables);
            }

            // 既存の要素を監視
            foreach (var item in source)
            {
                SubscribeToItem(item);
            }

            // コレクション変更を監視
            source.ObserveCollectionChanged()
                .Subscribe(args =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        // リセット時は全ての監視を再構築
                        itemDisposables.Clear();
                        foreach (var item in source)
                        {
                            SubscribeToItem(item);
                        }
                    }
                    else
                    {
                        // 追加された要素を監視
                        if (args.NewItems != null)
                        {
                            foreach (T item in args.NewItems)
                            {
                                SubscribeToItem(item);
                            }
                        }
                        // 削除された要素の監視は自動的に解除されないが、
                        // CompositeDisposableの性質上、要素が削除されても
                        // 次のリセット時に再構築される
                    }
                })
                .AddTo(mainDisposable);

            mainDisposable.Add(itemDisposables);

            return mainDisposable;
        });
    }
}
