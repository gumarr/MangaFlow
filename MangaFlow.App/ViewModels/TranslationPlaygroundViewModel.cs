using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using MangaFlow.AI;

namespace MangaFlow.App.ViewModels;

public partial class TranslationPlaygroundViewModel : ObservableObject
{
    private readonly ITranslationService _translationService;
    private readonly IProjectService _projectService;
    private readonly LlamaCppTranslationProvider _provider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TranslationPlaygroundViewModel> _logger;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _executionTimeText = "—";

    [ObservableProperty]
    private string _tokensPerSecText = "—";

    [ObservableProperty]
    private string _cacheStatusText = "—";

    [ObservableProperty]
    private string _modelNameText = "—";

    [ObservableProperty]
    private string _contextLengthText = "—";

    [ObservableProperty]
    private string _glossaryTermsUsedText = "—";

    [ObservableProperty]
    private string _bubbleCountText = "—";

    [ObservableProperty]
    private string _promptSizeText = "—";

    [ObservableProperty]
    private string _modelStatusText = "Not loaded";

    [ObservableProperty]
    private bool _isModelLoaded;

    public TranslationPlaygroundViewModel(
        ITranslationService translationService,
        IProjectService projectService,
        LlamaCppTranslationProvider provider,
        ISettingsService settingsService,
        ILogger<TranslationPlaygroundViewModel> logger)
    {
        _translationService = translationService;
        _projectService = projectService;
        _provider = provider;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        RefreshModelStatus();
        var settings = await _settingsService.GetSettingsAsync();
        ModelNameText = string.IsNullOrWhiteSpace(settings?.SelectedLlmModel) ? "Qwen3-8B Q4_K_M" : settings.SelectedLlmModel;
        ContextLengthText = $"{settings?.ContextSize ?? 2048} tokens";
    }

    private void RefreshModelStatus()
    {
        IsModelLoaded = _provider.IsModelLoaded;
        if (_provider.IsModelLoaded)
        {
            ModelStatusText = string.IsNullOrEmpty(_provider.GpuWarning)
                ? $"Loaded — {_provider.ActiveBackend}"
                : $"Loaded — {_provider.ActiveBackend} (GPU unavailable, see Settings)";
            ContextLengthText = $"{_provider.ActiveContextSize} tokens";
        }
        else
        {
            ModelStatusText = "Not loaded — set model path in Settings";
        }
    }

    [RelayCommand]
    public async Task LoadModelAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings?.LlmModelPath))
            {
                ModelStatusText = "No model path configured in Settings.";
                return;
            }
            if (!System.IO.File.Exists(settings.LlmModelPath))
            {
                ModelStatusText = $"File not found: {settings.LlmModelPath}";
                return;
            }

            ModelStatusText = "Loading model...";
            await _provider.EnsureModelLoadedAsync(
                settings.LlmModelPath,
                settings.CpuThreads,
                settings.UseGpu,
                (float)settings.Temperature,
                settings.GpuLayerCount,
                settings.ContextSize);

            RefreshModelStatus();
        }
        catch (Exception ex)
        {
            ModelStatusText = $"Load failed: {ex.Message}";
            _logger.LogError(ex, "Failed to load model from playground");
        }
    }

    [RelayCommand]
    public async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText))
            return;

        IsTranslating = true;
        TranslatedText = string.Empty;

        var sw = Stopwatch.StartNew();
        try
        {
            // Get active project for glossary context
            var projects = (await _projectService.GetAllProjectsAsync()).ToList();
            var project = projects.Count > 0 ? projects[0] : null;
            var projectId = project?.Id ?? Guid.Empty;
            var sourceLang = project?.SourceLanguage ?? "English";
            var targetLang = project?.TargetLanguage ?? "Vietnamese";

            // Build diagnostics preview
            var userPrompt = TranslationPromptBuilder.BuildUserPrompt(
                SourceText,
                new TranslationContext());
            PromptSizeText = $"{userPrompt.Length} chars";

            var prevHits = _provider.CacheHits;
            var result = await _translationService.TranslateAsync(
                projectId, SourceText, sourceLang, targetLang);

            sw.Stop();

            // Surface provider failures to the user instead of showing a blank box
            if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                TranslatedText = $"[Translation failed] {result.ErrorMessage}";
                ExecutionTimeText = $"{sw.ElapsedMilliseconds} ms";
                CacheStatusText = "ERROR";
                return;
            }

            TranslatedText = result.TranslatedText;
            ExecutionTimeText = $"{sw.ElapsedMilliseconds} ms";

            bool wasCacheHit = _provider.CacheHits > prevHits || result.ProviderName.Contains("cache") || result.ProviderName == "TranslationMemory";
            CacheStatusText = wasCacheHit ? "HIT" : "MISS";

            if (!wasCacheHit && sw.ElapsedMilliseconds > 0)
            {
                // Rough estimate: assume ~200 tokens output
                double tps = 200.0 / (sw.ElapsedMilliseconds / 1000.0);
                TokensPerSecText = $"~{tps:F1} tok/s";
            }
            else
            {
                TokensPerSecText = wasCacheHit ? "N/A (cache)" : "—";
            }

            RefreshModelStatus();
        }
        catch (Exception ex)
        {
            sw.Stop();
            TranslatedText = $"Error: {ex.Message}";
            ExecutionTimeText = $"{sw.ElapsedMilliseconds} ms";
            _logger.LogError(ex, "Translation playground failed");
        }
        finally
        {
            IsTranslating = false;
        }
    }

    [RelayCommand]
    public void ClearCache()
    {
        CacheStatusText = "—";
        _logger.LogInformation("Translation cache clear requested from playground");
    }
}
