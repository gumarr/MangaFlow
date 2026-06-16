using CommunityToolkit.Mvvm.ComponentModel;

namespace MangaFlow.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _appTitle = "MangaFlow";
}
