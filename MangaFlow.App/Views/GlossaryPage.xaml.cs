using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MangaFlow.App.ViewModels;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.Views;

public sealed partial class GlossaryPage : Page
{
    public GlossaryViewModel ViewModel { get; }

    public GlossaryPage()
    {
        InitializeComponent();
        
        ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).ServiceProvider.GetRequiredService<GlossaryViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        await ViewModel.InitializeAsync();
        
        // Populate ScopeComboBox using the ToString wrapper
        var items = new List<ProjectScopeWrapper> { new(null) };
        foreach (var p in ViewModel.Projects)
        {
            if (p != null)
            {
                items.Add(new ProjectScopeWrapper(p));
            }
        }
        
        ScopeComboBox.ItemsSource = items;
        ScopeComboBox.SelectedIndex = 0;
    }

    private async void OnScopeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScopeComboBox.SelectedItem is ProjectScopeWrapper selectedWrapper)
        {
            ViewModel.SelectedProject = selectedWrapper.Project;
            await ViewModel.LoadTermsAsync();
        }
    }

    private async void OnDeleteTermClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is GlossaryTerm term)
        {
            await ViewModel.DeleteTermCommand.ExecuteAsync(term);
        }
    }

    public class ProjectScopeWrapper
    {
        public Project? Project { get; }
        
        public ProjectScopeWrapper(Project? project)
        {
            Project = project;
        }

        public override string ToString()
        {
            return Project?.Name ?? "Global Glossary (Shared)";
        }
    }
}
