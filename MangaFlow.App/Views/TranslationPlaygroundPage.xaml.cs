using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class TranslationPlaygroundPage : Page
{
    public TranslationPlaygroundViewModel ViewModel { get; }

    public TranslationPlaygroundPage()
    {
        InitializeComponent();
        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<TranslationPlaygroundViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }
}
