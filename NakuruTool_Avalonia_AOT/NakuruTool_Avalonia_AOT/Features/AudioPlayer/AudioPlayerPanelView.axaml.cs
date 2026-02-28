using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

public partial class AudioPlayerPanelView : UserControl
{
    public AudioPlayerPanelView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Slider内部のTrack要素がPointerイベントをHandledにするため、
        // Tunnelストラテジーで先にイベントを捕捉する
        SeekBar.AddHandler(InputElement.PointerPressedEvent, SeekBar_PointerPressed, RoutingStrategies.Tunnel);
        SeekBar.AddHandler(InputElement.PointerReleasedEvent, SeekBar_PointerReleased, RoutingStrategies.Tunnel);
    }

    private void SeekBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AudioPlayerPanelViewModel vm)
            vm.IsSeeking = true;
    }

    private void SeekBar_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is AudioPlayerPanelViewModel vm)
        {
            // SeekCommand を先に実行してシーク対象位置を確定させ、
            // その後に IsSeeking = false でポーリングを再開する。
            // 逆順だとポーリングが古い位置で CurrentPosition を上書きし、
            // シークコマンドが古い位置でシークしてしまう（後退シークで顕著）。
            vm.SeekCommand.Execute(null);
            vm.IsSeeking = false;
        }
    }
}
