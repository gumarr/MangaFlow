using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        
        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<SettingsViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (ViewModel.LoadSettingsCommand.CanExecute(null))
        {
            await ViewModel.LoadSettingsCommand.ExecuteAsync(null);
        }
    }
}
