using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ITranslationHistoryRepository _historyRepository;
    private readonly IProjectService _projectService;
    private readonly ILogger<HistoryViewModel> _logger;

    [ObservableProperty]
    private Project? _selectedProject;

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<TranslationHistoryItem> HistoryItems { get; } = new();

    public HistoryViewModel(
        ITranslationHistoryRepository historyRepository,
        IProjectService projectService,
        ILogger<HistoryViewModel> logger)
    {
        _historyRepository = historyRepository;
        _projectService = projectService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing HistoryViewModel...");
        try
        {
            var projectList = await _projectService.GetAllProjectsAsync();
            Projects.Clear();
            foreach (var p in projectList)
            {
                Projects.Add(p);
            }

            if (Projects.Count > 0)
            {
                SelectedProject = Projects[0];
                await LoadHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize HistoryViewModel");
        }
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (SelectedProject == null)
        {
            HistoryItems.Clear();
            return;
        }

        _logger.LogInformation("Loading history for project: {ProjectId}", SelectedProject.Id);
        try
        {
            var list = await _historyRepository.GetByProjectIdAsync(SelectedProject.Id);
            HistoryItems.Clear();
            foreach (var item in list)
            {
                HistoryItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history items");
        }
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        if (SelectedProject == null) return;

        _logger.LogInformation("Clearing history for project: {ProjectId}", SelectedProject.Id);
        try
        {
            await _historyRepository.ClearProjectHistoryAsync(SelectedProject.Id);
            HistoryItems.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history");
        }
    }
}
