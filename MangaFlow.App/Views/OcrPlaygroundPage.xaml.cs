using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class OcrPlaygroundPage : Page
{
    public OcrPlaygroundViewModel ViewModel { get; }

    public OcrPlaygroundPage()
    {
        InitializeComponent();

        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<OcrPlaygroundViewModel>();
        this.DataContext = ViewModel;
    }
}
