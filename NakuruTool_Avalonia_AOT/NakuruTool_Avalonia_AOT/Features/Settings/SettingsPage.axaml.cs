using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NakuruTool_Avalonia_AOT.Features.Translate;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.Settings;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    // ボタンクリック時に発生するイベントハンドラ
    private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        // DataContextからViewModelのインスタンスを取得して確認
        if (DataContext is SettingsViewModel viewModel)
        {
            // 最上位ウィンドウ（TopLevelコントロール）からIStorageProviderを取得する。
            var storageProvider = TopLevel.GetTopLevel(this)!.StorageProvider;

            // ViewModelのメソッドを呼び出し、storageProviderを渡す。
            var selectedFilePath = await OpenFileAndGetFolderPath(storageProvider);
            if (selectedFilePath != null &&
                selectedFilePath.TryGetLocalPath() is { } localPath)
            {
                // ファイルのフルパスから親ディレクトリのパスを取り出す。
                viewModel.SelectedFolderPath = Path.GetDirectoryName(localPath)!;
            }
        }
    }

    /// <summary>
    /// ファイル選択ダイアログを開き、選択したosu!.exeファイルの親フォルダパスを返す。
    /// </summary>
    /// <param name="storageProvider">IStorageProviderインスタンス</param>
    private async Task<IStorageFile?> OpenFileAndGetFolderPath(IStorageProvider storageProvider)
    {
        var filePickerOpenOptions = new FilePickerOpenOptions
        {
            Title = LanguageService.Instance.GetString("Settings.PleaseChooseOsuPath"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("") { Patterns = new[] { "osu!.exe" } }
            }
        };

        // ユーザーが選択したファイルのリストを取得する。
        var result = await storageProvider.OpenFilePickerAsync(filePickerOpenOptions);

        if (result.Any())
        {
            return result.First();
        }

        return null;
    }
}
