using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.Shared.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public LanguageService LangServiceInstance { get; } = LanguageService.Instance;

        protected CompositeDisposable Disposables { get; } = new CompositeDisposable();

        public virtual void Dispose()
        {
            Disposables.Dispose();
        }
    }
}
