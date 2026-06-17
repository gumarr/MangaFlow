using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

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
    private string _detectorPathText = string.Empty;

    [ObservableProperty]
    private string _recognizerPathText = string.Empty;

    [ObservableProperty]
    private string _dictionaryPathText = string.Empty;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _languageText = string.Empty;

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
        UpdateDiagnostics();
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
            UpdateDiagnostics();
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task LoadLastCaptureAsync()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string captureInputPath = Path.Combine(localAppData, "MangaFlow", "debug", "capture_input.png");
            
            if (!File.Exists(captureInputPath))
            {
                OcrResultText = "No previous capture_input.png found. Please run screen capture first.";
                return;
            }

            SelectedImagePath = captureInputPath;

            // Load preview
            var bitmap = new BitmapImage();
            using (var stream = File.OpenRead(captureInputPath))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }
            SelectedImagePreview = bitmap;
            OcrResultText = "Loaded capture_input.png. Click 'Run OCR' to run.";
            ExecutionTimeText = string.Empty;
            ConfidenceText = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load last capture input.");
            OcrResultText = $"Failed to load capture_input.png: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RunTestCaseAsync(string testType)
    {
        string text = testType switch
        {
            "Simple" => "Hello, this is a simple English test.",
            "Paragraph" => "MangaFlow v1.0 features English OCR and Vietnamese translation support.",
            "Mixed" => "Let's test numbers: 12345, and symbols: !@#$%^&*()_+.",
            "Uppercase" => "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.",
            _ => "This is a default test."
        };

        try
        {
            string path = GenerateTestImage(text);
            SelectedImagePath = path;

            // Load preview
            var bitmap = new BitmapImage();
            using (var stream = File.OpenRead(path))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }
            SelectedImagePreview = bitmap;

            // Automatically trigger OCR execution
            await RunOcrAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run test case: {TestType}", testType);
            OcrResultText = $"Failed to generate test image: {ex.Message}";
        }
    }

    private string GenerateTestImage(string text)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "MangaFlow");
        if (!Directory.Exists(tempFolder))
        {
            Directory.CreateDirectory(tempFolder);
        }

        var tempPath = Path.Combine(tempFolder, $"test_{Guid.NewGuid()}.png");

        using (var bitmap = new SKBitmap(600, 150))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            using (var paint = new SKPaint())
            using (var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 24))
            {
                paint.Color = SKColors.Black;
                paint.IsAntialias = true;

                // Draw background box/border for contrast
                using (var borderPaint = new SKPaint())
                {
                    borderPaint.Color = SKColors.LightGray;
                    borderPaint.Style = SKPaintStyle.Stroke;
                    borderPaint.StrokeWidth = 2;
                    canvas.DrawRect(new SKRect(10, 10, 590, 140), borderPaint);
                }

                // Draw the text in the middle
                canvas.DrawText(text, 30, 85, font, paint);
            }

            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(tempPath))
            {
                data.SaveTo(stream);
            }
        }

        return tempPath;
    }

    private void UpdateDiagnostics()
    {
        var diag = _ocrService.GetDiagnostics();
        if (diag != null)
        {
            DetectorPathText = diag.DetectorPath;
            RecognizerPathText = diag.RecognizerPath;
            DictionaryPathText = diag.DictionaryPath;
            SourceText = diag.Source;
            LanguageText = diag.Language;
        }
        else
        {
            DetectorPathText = "Not Initialized";
            RecognizerPathText = "Not Initialized";
            DictionaryPathText = "Not Initialized";
            SourceText = "Not Initialized";
            LanguageText = "Not Initialized";
        }
    }
}
