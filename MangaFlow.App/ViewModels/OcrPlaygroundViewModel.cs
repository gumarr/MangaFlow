using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MangaFlow.App.ViewModels;

public partial class OcrPlaygroundViewModel : ObservableObject
{
    private readonly IOcrService _ocrService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OcrPlaygroundViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunOcrCommand))]
    private string _selectedImagePath = string.Empty;

    [ObservableProperty]
    private string _ocrResultText = string.Empty;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _confidenceText = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private BitmapImage? _selectedImagePreview;

    public OcrPlaygroundViewModel(
        IOcrService ocrService,
        ISettingsService settingsService,
        ILogger<OcrPlaygroundViewModel> logger)
    {
        _ocrService = ocrService;
        _settingsService = settingsService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task SelectImageAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                SelectedImagePath = file.Path;

                // Load preview
                var bitmap = new BitmapImage();
                using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    await bitmap.SetSourceAsync(stream);
                }
                SelectedImagePreview = bitmap;
                OcrResultText = string.Empty;
                ExecutionTimeText = string.Empty;
                ConfidenceText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pick image file.");
        }
    }

    private bool CanRunOcr => !string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath);

    [RelayCommand(CanExecute = nameof(CanRunOcr))]
    public async Task RunOcrAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedImagePath) || !File.Exists(SelectedImagePath)) return;

        IsRunning = true;
        OcrResultText = "Processing image with OCR...";
        ExecutionTimeText = string.Empty;
        ConfidenceText = string.Empty;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            string language = settings?.OcrLanguage ?? "Japanese";

            var ocrResult = await _ocrService.RecognizeTextAsync(SelectedImagePath, language);
            stopwatch.Stop();

            if (ocrResult == null || (ocrResult.Lines.Count == 0 && ocrResult.Blocks.Count == 0))
            {
                OcrResultText = "No text detected in image.";
                ConfidenceText = "Confidence: N/A";
            }
            else
            {
                OcrResultText = ocrResult.FullText;

                // Calculate average confidence
                float avgConf = 0f;
                int count = 0;

                if (ocrResult.Lines.Count > 0)
                {
                    avgConf = ocrResult.Lines.Average(l => l.Confidence);
                    count = ocrResult.Lines.Count;
                }
                else if (ocrResult.Blocks.Count > 0)
                {
                    avgConf = ocrResult.Blocks.Average(b => b.Confidence);
                    count = ocrResult.Blocks.Count;
                }

                ConfidenceText = $"Confidence: {avgConf * 100:F1}% (from {count} segments)";
            }

            ExecutionTimeText = $"Execution Time: {stopwatch.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to execute OCR during playground test.");
            OcrResultText = $"OCR Failed: {ex.Message}";
            ExecutionTimeText = $"Execution Time: {stopwatch.ElapsedMilliseconds} ms";
            ConfidenceText = "Confidence: 0.0%";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
