using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        
        // Resolve from App ServiceProvider
        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<HomeViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // Reload projects
        if (ViewModel.LoadProjectsCommand.CanExecute(null))
        {
            ViewModel.LoadProjectsCommand.Execute(null);
        }

        // Run OCR startup validation
        if (ViewModel.CheckOcrStatusCommand.CanExecute(null))
        {
            await ViewModel.CheckOcrStatusCommand.ExecuteAsync(null);
        }
    }
}
