using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class SingleBeatmapGenerationOverlayView : UserControl
{
    public static readonly StyledProperty<SingleBeatmapGenerationViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<SingleBeatmapGenerationOverlayView, SingleBeatmapGenerationViewModel?>(nameof(ViewModel));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<SingleBeatmapGenerationOverlayView, ICommand?>(nameof(CloseCommand));

    public SingleBeatmapGenerationViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public SingleBeatmapGenerationOverlayView()
    {
        InitializeComponent();
    }
}
