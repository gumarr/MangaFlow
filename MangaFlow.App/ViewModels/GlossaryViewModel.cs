using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class GlossaryViewModel : ObservableObject
{
    private readonly IGlossaryService _glossaryService;
    private readonly IProjectService _projectService;
    private readonly ILogger<GlossaryViewModel> _logger;

    [ObservableProperty]
    private Project? _selectedProject; // Null represents Global Glossary

    [ObservableProperty]
    private string _newSourceText = string.Empty;

    [ObservableProperty]
    private string _newTargetText = string.Empty;

    [ObservableProperty]
    private bool _newIsLocked;

    public ObservableCollection<Project?> Projects { get; } = new();
    public ObservableCollection<GlossaryTerm> Terms { get; } = new();

    public GlossaryViewModel(
        IGlossaryService glossaryService,
        IProjectService projectService,
        ILogger<GlossaryViewModel> logger)
    {
        _glossaryService = glossaryService;
        _projectService = projectService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing GlossaryViewModel...");
        try
        {
            var projectList = await _projectService.GetAllProjectsAsync();
            Projects.Clear();
            
            // First item represents Global Glossary
            Projects.Add(null);
            
            foreach (var p in projectList)
            {
                Projects.Add(p);
            }

            SelectedProject = null; // Default to Global Glossary
            await LoadTermsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GlossaryViewModel");
        }
    }

    [RelayCommand]
    public async Task LoadTermsAsync()
    {
        _logger.LogInformation("Loading terms. SelectedProject is null: {IsGlobal}", SelectedProject == null);
        try
        {
            IEnumerable<GlossaryTerm> list;
            if (SelectedProject == null)
            {
                list = await _glossaryService.GetGlobalGlossaryAsync();
            }
            else
            {
                list = await _glossaryService.GetProjectGlossaryAsync(SelectedProject.Id);
            }

            Terms.Clear();
            foreach (var term in list)
            {
                Terms.Add(term);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load glossary terms");
        }
    }

    [RelayCommand]
    public async Task AddTermAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSourceText) || string.IsNullOrWhiteSpace(NewTargetText)) return;

        _logger.LogInformation("Adding term '{Source}' -> '{Target}'", NewSourceText, NewTargetText);
        try
        {
            Guid? projectId = SelectedProject?.Id;
            await _glossaryService.AddTermAsync(projectId, NewSourceText, NewTargetText, NewIsLocked);
            
            // Clear fields
            NewSourceText = string.Empty;
            NewTargetText = string.Empty;
            NewIsLocked = false;

            await LoadTermsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add glossary term");
        }
    }

    [RelayCommand]
    public async Task DeleteTermAsync(GlossaryTerm? term)
    {
        if (term == null) return;

        _logger.LogInformation("Deleting term ID: {TermId}", term.Id);
        try
        {
            await _glossaryService.DeleteTermAsync(term.Id);
            Terms.Remove(term);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete glossary term");
        }
    }

    [RelayCommand]
    public async Task ImportGlossaryAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            await _glossaryService.ImportGlossaryAsync(SelectedProject?.Id, filePath);
            await LoadTermsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import glossary from: {FilePath}", filePath);
        }
    }

    [RelayCommand]
    public async Task ExportGlossaryAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            await _glossaryService.ExportGlossaryAsync(SelectedProject?.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export glossary to: {FilePath}", filePath);
        }
    }
}
