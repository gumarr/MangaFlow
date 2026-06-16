using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;

namespace MangaFlow.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private AppSettings? _settings;

    [ObservableProperty]
    private string _ocrLanguage = "Japanese";

    [ObservableProperty]
    private string _selectedLlmModel = "Qwen 3 8B GGUF";

    [ObservableProperty]
    private string _llmModelPath = string.Empty;

    [ObservableProperty]
    private int _cpuThreads = 4;

    [ObservableProperty]
    private double _temperature = 0.3;

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Shift+T";

    [ObservableProperty]
    private string _defaultSourceLanguage = "Japanese";

    [ObservableProperty]
    private string _defaultTargetLanguage = "English";

    [ObservableProperty]
    private bool _isSaving;

    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
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
            SelectedLlmModel = appSettings.SelectedLlmModel;
            LlmModelPath = appSettings.LlmModelPath;
            CpuThreads = appSettings.CpuThreads;
            Temperature = appSettings.Temperature;
            UseGpu = appSettings.UseGpu;
            GlobalHotkey = appSettings.GlobalHotkey;
            DefaultSourceLanguage = appSettings.DefaultSourceLanguage;
            DefaultTargetLanguage = appSettings.DefaultTargetLanguage;
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
            Settings.SelectedLlmModel = SelectedLlmModel;
            Settings.LlmModelPath = LlmModelPath;
            Settings.CpuThreads = CpuThreads;
            Settings.Temperature = Temperature;
            Settings.UseGpu = UseGpu;
            Settings.GlobalHotkey = GlobalHotkey;
            Settings.DefaultSourceLanguage = DefaultSourceLanguage;
            Settings.DefaultTargetLanguage = DefaultTargetLanguage;

            await _settingsService.UpdateSettingsAsync(Settings);
            _logger.LogInformation("Settings saved successfully.");
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
