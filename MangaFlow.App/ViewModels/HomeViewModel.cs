using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ITranslationService _translationService;
    private readonly IOcrService _ocrService;
    private readonly IProjectService _projectService;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<HomeViewModel> _logger;

    [ObservableProperty]
    private string _recognizedText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string? _ocrValidationMessage;

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Shift+T";

    public ObservableCollection<Project> Projects { get; } = new();

    public HomeViewModel(
        ITranslationService translationService,
        IOcrService ocrService,
        IProjectService projectService,
        IScreenCaptureService screenCaptureService,
        ISettingsService settingsService,
        ILogger<HomeViewModel> logger)
    {
        _translationService = translationService;
        _ocrService = ocrService;
        _projectService = projectService;
        _screenCaptureService = screenCaptureService;
        _settingsService = settingsService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadProjectsAsync()
    {
        _logger.LogInformation("Loading projects in HomeView...");
        try
        {
            var list = await _projectService.GetAllProjectsAsync();
            Projects.Clear();
            foreach (var proj in list)
            {
                Projects.Add(proj);
            }

            if (Projects.Count > 0 && SelectedProject == null)
            {
                SelectedProject = Projects[0];
            }

            var settings = await _settingsService.GetSettingsAsync();
            GlobalHotkey = settings.GlobalHotkey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading projects in HomeView");
        }
    }

    [RelayCommand]
    public async Task CheckOcrStatusAsync()
    {
        try
        {
            OcrValidationMessage = await _ocrService.ValidateAsync();
            if (OcrValidationMessage != null)
            {
                _logger.LogWarning("OCR startup validation failed: {Message}", OcrValidationMessage);
            }
            else
            {
                _logger.LogInformation("OCR startup validation passed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running OCR startup validation");
        }
    }

    [RelayCommand]
    public async Task TranslateRegionAsync()
    {
        if (SelectedProject == null)
        {
            RecognizedText = "Please select or create a project first!";
            return;
        }

        IsTranslating = true;
        RecognizedText = "Capturing screen region...";
        TranslatedText = string.Empty;

        try
        {
            // Simulate capturing screen region
            var imageBytes = await _screenCaptureService.CaptureScreenRegionAsync(100, 100, 300, 150);
            
            RecognizedText = "Running OCR scan...";
            var ocrResult = await _ocrService.RecognizeTextAsync(imageBytes, SelectedProject.SourceLanguage);
            RecognizedText = ocrResult.FullText;

            if (string.IsNullOrWhiteSpace(RecognizedText))
            {
                RecognizedText = "[No text recognized]";
                IsTranslating = false;
                return;
            }

            RecognizedText = ocrResult.FullText;
            TranslatedText = "Running local LLM translation...";
            
            var translationResult = await _translationService.TranslateAsync(
                SelectedProject.Id,
                ocrResult.FullText,
                SelectedProject.SourceLanguage,
                SelectedProject.TargetLanguage);

            TranslatedText = translationResult.TranslatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation pipeline failed");
            TranslatedText = $"Error: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }
}
