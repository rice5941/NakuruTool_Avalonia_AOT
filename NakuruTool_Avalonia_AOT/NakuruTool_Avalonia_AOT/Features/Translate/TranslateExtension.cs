using Avalonia;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.Translate
{
    /// <summary>
    /// XAML用の翻訳マークアップ拡張
    /// NativeAOT対応：リフレクションを完全に避け、直接翻訳値を返す
    /// </summary>
    public class TranslateExtension : MarkupExtension
    {
        /// <summary>
        /// 翻訳キー
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// キーを指定するコンストラクタ
        /// </summary>
        /// <param name="key">翻訳キー</param>
        public TranslateExtension(string key)
        {
            Key = key;
        }

        /// <summary>
        /// マークアップ拡張の値を提供する
        /// </summary>
        /// <param name="serviceProvider">サービスプロバイダー</param>
        /// <returns>翻訳値（文字列を直接返す）</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return string.Empty;
            }

            // IProvideValueTargetを取得してターゲットオブジェクトを特定
            var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            
            if (provideValueTarget?.TargetObject is AvaloniaObject targetObject &&
                provideValueTarget?.TargetProperty is AvaloniaProperty targetProperty)
            {
                // WeakEventManagerを通じてイベント登録（メモリリーク防止）
                TranslateWeakEventManager.Register(targetObject, targetProperty, Key);
            }

            // 現在の翻訳値を直接返す
            return LanguageService.Instance.GetString(Key);
        }
    }

    /// <summary>
    /// 翻訳イベント用のWeakEventManager
    /// ターゲットオブジェクトが破棄されたら自動的にイベント購読を解除
    /// </summary>
    internal static class TranslateWeakEventManager
    {
        private static readonly object _lock = new();
        private static readonly List<WeakSubscription> _subscriptions = new();
        private static bool _isSubscribed;

        /// <summary>
        /// 翻訳更新対象を登録
        /// </summary>
        public static void Register(AvaloniaObject target, AvaloniaProperty property, string key)
        {
            lock (_lock)
            {
                // LanguageChangedイベントへの購読（初回のみ）
                if (!_isSubscribed)
                {
                    LanguageService.Instance.LanguageChanged += OnLanguageChanged;
                    _isSubscribed = true;
                }

                // 新しいサブスクリプションを追加
                _subscriptions.Add(new WeakSubscription(target, property, key));
            }
        }

        /// <summary>
        /// 言語変更時のハンドラー
        /// </summary>
        private static void OnLanguageChanged(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                // 有効なサブスクリプションのみ処理し、無効なものは削除
                for (int i = _subscriptions.Count - 1; i >= 0; i--)
                {
                    var subscription = _subscriptions[i];
                    
                    if (subscription.TryUpdate())
                    {
                        // 更新成功 - 何もしない
                    }
                    else
                    {
                        // ターゲットが破棄済み - リストから削除
                        _subscriptions.RemoveAt(i);
                    }
                }

                // 全てのサブスクリプションが削除されたらイベント購読も解除
                if (_subscriptions.Count == 0 && _isSubscribed)
                {
                    LanguageService.Instance.LanguageChanged -= OnLanguageChanged;
                    _isSubscribed = false;
                }
            }
        }

        /// <summary>
        /// 弱い参照を使ったサブスクリプション情報
        /// </summary>
        private sealed class WeakSubscription
        {
            private readonly WeakReference<AvaloniaObject> _weakTarget;
            private readonly AvaloniaProperty _property;
            private readonly string _key;

            public WeakSubscription(AvaloniaObject target, AvaloniaProperty property, string key)
            {
                _weakTarget = new WeakReference<AvaloniaObject>(target);
                _property = property;
                _key = key;
            }

            /// <summary>
            /// ターゲットの翻訳を更新
            /// </summary>
            /// <returns>更新成功ならtrue、ターゲットが破棄済みならfalse</returns>
            public bool TryUpdate()
            {
                if (_weakTarget.TryGetTarget(out var target))
                {
                    var newValue = LanguageService.Instance.GetString(_key);
                    target.SetValue(_property, newValue);
                    return true;
                }
                return false;
            }
        }
    }
}