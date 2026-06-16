using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        
        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<HistoryViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        await ViewModel.InitializeAsync();
    }

    private async void OnProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.LoadHistoryCommand.CanExecute(null))
        {
            await ViewModel.LoadHistoryCommand.ExecuteAsync(null);
        }
    }
}
