using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.AI;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly Services.CaptureWorkflow _captureWorkflow;
    private readonly ITranslationMemoryRepository _tmRepository;
    private readonly LlamaCppTranslationProvider _llmProvider;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private AppSettings? _settings;

    [ObservableProperty]
    private string _ocrLanguage = "English";

    [ObservableProperty]
    private string _ocrEngine = "RapidOCR";

    [ObservableProperty]
    private string _ocrModelPath = string.Empty;

    [ObservableProperty]
    private string _selectedLlmModel = "Qwen 3 8B GGUF";

    [ObservableProperty]
    private string _llmModelPath = string.Empty;

    [ObservableProperty]
    private int _cpuThreads = 4;

    [ObservableProperty]
    private double _temperature = 0.3;

    [ObservableProperty]
    private bool _useGpu = false;

    [ObservableProperty]
    private int _gpuLayerCount = 99;

    [ObservableProperty]
    private int _contextSize = 2048;

    [ObservableProperty]
    private string _backendStatusText = "Not loaded";

    [ObservableProperty]
    private bool _isReloadingModel;

    [ObservableProperty]
    private string _globalHotkey = "Alt + Q";

    [ObservableProperty]
    private string _defaultSourceLanguage = "English";

    [ObservableProperty]
    private string _defaultTargetLanguage = "Vietnamese";

    [ObservableProperty]
    private bool _showCapturePreview = false;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _clearTmStatusText = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeyService hotkeyService,
        Services.CaptureWorkflow captureWorkflow,
        ITranslationMemoryRepository tmRepository,
        LlamaCppTranslationProvider llmProvider,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _captureWorkflow = captureWorkflow;
        _tmRepository = tmRepository;
        _llmProvider = llmProvider;
        _logger = logger;
        BackendStatusText = _llmProvider.IsModelLoaded ? $"Running on: {_llmProvider.ActiveBackend}" : "Not loaded";
    }

    [RelayCommand]
    public async Task ReloadModelAsync()
    {
        if (Settings == null) return;
        if (string.IsNullOrWhiteSpace(LlmModelPath) || !System.IO.File.Exists(LlmModelPath))
        {
            BackendStatusText = "Model file not found — set a valid .gguf path first.";
            return;
        }

        IsReloadingModel = true;
        BackendStatusText = "Loading model...";
        try
        {
            // Persist current values first so the load uses what the user sees.
            await SaveSettingsAsync();

            await _llmProvider.EnsureModelLoadedAsync(
                LlmModelPath,
                CpuThreads,
                UseGpu,
                (float)Temperature,
                GpuLayerCount,
                ContextSize);

            BackendStatusText = $"Running on: {_llmProvider.ActiveBackend} | context {_llmProvider.ActiveContextSize}";

            // If GPU was requested but the CUDA runtime is missing, tell the user why
            // it fell back to CPU and how to fix it.
            if (!string.IsNullOrEmpty(_llmProvider.GpuWarning))
            {
                BackendStatusText += $"\n⚠ {_llmProvider.GpuWarning}";
            }

            _logger.LogInformation("Model reloaded from Settings. Backend: {Backend}", _llmProvider.ActiveBackend);
        }
        catch (Exception ex)
        {
            BackendStatusText = $"Load failed: {ex.Message}";
            _logger.LogError(ex, "Failed to reload model from Settings");
        }
        finally
        {
            IsReloadingModel = false;
        }
    }

    [RelayCommand]
    public async Task ClearTranslationMemoryAsync()
    {
        try
        {
            await _tmRepository.ClearAllAsync();
            ClearTmStatusText = "Translation memory cleared.";
            _logger.LogInformation("Translation memory cleared by user.");
        }
        catch (Exception ex)
        {
            ClearTmStatusText = $"Error: {ex.Message}";
            _logger.LogError(ex, "Failed to clear translation memory");
        }
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        _logger.LogInformation("Loading settings in SettingsViewModel...");
        try
        {
            var appSettings = await _settingsService.GetSettingsAsync();
            Settings = appSettings;

            // Load properties into view fields
            OcrLanguage = appSettings.OcrLanguage;
            OcrEngine = appSettings.OcrEngine;
            OcrModelPath = appSettings.OcrModelPath;
            SelectedLlmModel = appSettings.SelectedLlmModel;
            LlmModelPath = appSettings.LlmModelPath;
            CpuThreads = appSettings.CpuThreads;
            Temperature = appSettings.Temperature;
            UseGpu = appSettings.UseGpu;
            GpuLayerCount = appSettings.GpuLayerCount;
            ContextSize = appSettings.ContextSize;
            GlobalHotkey = appSettings.GlobalHotkey;
            DefaultSourceLanguage = appSettings.DefaultSourceLanguage;
            DefaultTargetLanguage = appSettings.DefaultTargetLanguage;
            ShowCapturePreview = appSettings.ShowCapturePreview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (Settings == null) return;

        IsSaving = true;
        _logger.LogInformation("Saving settings in SettingsViewModel...");
        try
        {
            // Sync fields back to entity
            Settings.OcrLanguage = OcrLanguage;
            Settings.OcrEngine = OcrEngine;
            Settings.OcrModelPath = OcrModelPath;
            Settings.SelectedLlmModel = SelectedLlmModel;
            Settings.LlmModelPath = LlmModelPath;
            Settings.CpuThreads = CpuThreads;
            Settings.Temperature = Temperature;
            Settings.UseGpu = UseGpu;
            Settings.GpuLayerCount = GpuLayerCount;
            Settings.ContextSize = ContextSize;
            Settings.GlobalHotkey = GlobalHotkey;
            Settings.DefaultSourceLanguage = DefaultSourceLanguage;
            Settings.DefaultTargetLanguage = DefaultTargetLanguage;
            Settings.ShowCapturePreview = ShowCapturePreview;

            await _settingsService.UpdateSettingsAsync(Settings);
            _logger.LogInformation("Settings saved successfully. Re-registering hotkey...");

            _hotkeyService.RegisterHotkey(Settings.GlobalHotkey, () =>
            {
                var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (dispatcher != null)
                {
                    dispatcher.TryEnqueue(async () =>
                    {
                        await _captureWorkflow.StartCaptureWorkflowAsync();
                    });
                }
                else
                {
                    Task.Run(async () => await _captureWorkflow.StartCaptureWorkflowAsync());
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
        finally
        {
            IsSaving = false;
        }
    }
}
