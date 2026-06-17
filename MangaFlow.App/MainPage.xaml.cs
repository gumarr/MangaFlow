using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.Views;

namespace MangaFlow.App;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // Navigate to Home by default
        MainNavView.SelectedItem = HomeItem;
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            sender.Header = "Settings";
        }
        else if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Home":
                    ContentFrame.Navigate(typeof(HomePage));
                    sender.Header = "MangaFlow - Home";
                    break;
                case "Projects":
                    ContentFrame.Navigate(typeof(ProjectsPage));
                    sender.Header = "MangaFlow - Projects";
                    break;
                case "Glossary":
                    ContentFrame.Navigate(typeof(GlossaryPage));
                    sender.Header = "MangaFlow - Glossary";
                    break;
                case "History":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    sender.Header = "MangaFlow - History";
                    break;
                case "OcrPlayground":
                    ContentFrame.Navigate(typeof(OcrPlaygroundPage));
                    sender.Header = "MangaFlow - OCR Playground";
                    break;
                case "TranslationPlayground":
                    ContentFrame.Navigate(typeof(TranslationPlaygroundPage));
                    sender.Header = "MangaFlow - Translation Playground";
                    break;
            }
        }
    }
}
