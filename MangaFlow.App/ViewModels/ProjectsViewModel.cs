using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectSelected))]
    [NotifyPropertyChangedFor(nameof(IsProjectNotSelected))]
    private Project? _selectedProject;

    public bool IsProjectSelected => SelectedProject != null;
    public bool IsProjectNotSelected => SelectedProject == null;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _newProjectDescription = string.Empty;

    [ObservableProperty]
    private string _newProjectSourceLanguage = "Japanese";

    [ObservableProperty]
    private string _newProjectTargetLanguage = "English";

    [ObservableProperty]
    private string _newProjectFolderPath = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<Project> Projects { get; } = new();

    public ProjectsViewModel(IProjectService projectService, ILogger<ProjectsViewModel> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadProjectsAsync()
    {
        IsLoading = true;
        _logger.LogInformation("Loading projects...");
        try
        {
            var list = await _projectService.GetAllProjectsAsync();
            Projects.Clear();
            foreach (var p in list)
            {
                Projects.Add(p);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load projects");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        _logger.LogInformation("Creating project: {Name}", NewProjectName);
        try
        {
            var project = await _projectService.CreateProjectAsync(
                NewProjectName,
                NewProjectDescription,
                NewProjectSourceLanguage,
                NewProjectTargetLanguage,
                NewProjectFolderPath);

            Projects.Add(project);
            SelectedProject = project;

            // Clear inputs
            NewProjectName = string.Empty;
            NewProjectDescription = string.Empty;
            NewProjectFolderPath = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project");
        }
    }

    [RelayCommand]
    public async Task DeleteProjectAsync(Project? project)
    {
        if (project == null) return;

        _logger.LogInformation("Deleting project: {ProjectId}", project.Id);
        try
        {
            await _projectService.DeleteProjectAsync(project.Id);
            Projects.Remove(project);
            if (SelectedProject == project)
            {
                SelectedProject = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project");
        }
    }
}
