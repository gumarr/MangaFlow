using Microsoft.UI.Xaml;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class CaptureResultWindow : Window
{
    public CaptureResultViewModel ViewModel { get; }

    public CaptureResultWindow(CaptureResultViewModel viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
