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

    // ボタンがクリックされたときに呼び出されるイベントハンドラ
    private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        // DataContextが期待するViewModelのインスタンスであることを確認
        if (DataContext is SettingsViewModel viewModel)
        {
            // このウィンドウ（TopLevelコントロール）からIStorageProviderを取得します。
            var storageProvider = TopLevel.GetTopLevel(this)!.StorageProvider;

            // ViewModelのメソッドを呼び出し、storageProviderを渡します。
            var selectedFilePath = await OpenFileAndGetFolderPath(storageProvider);
            if (selectedFilePath != null &&
                selectedFilePath.TryGetLocalPath() is { } localPath)
            {
                // ファイルのフルパスから親ディレクトリのパスを抽出します。
                viewModel.SelectedFolderPath = Path.GetDirectoryName(localPath)!;
            }
        }
    }

    /// <summary>
    /// ファイル選択ダイアログを開き、選択された.exeファイルの親フォルダパスを取得します。
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

        // ユーザーが選択したファイルのリストが返されます。
        var result = await storageProvider.OpenFilePickerAsync(filePickerOpenOptions);

        if (result.Any())
        {
            return result.First();
        }

        return null;
    }
}
