using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.App.Views;
using MangaFlow.App.ViewModels;
using Microsoft.UI.Xaml;

namespace MangaFlow.App.Services;

public class CaptureWorkflow
{
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ILogger<CaptureWorkflow> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CaptureResultWindow? _activeResultWindow;

    public CaptureWorkflow(
        IScreenCaptureService screenCaptureService,
        IOcrService ocrService,
        ILogger<CaptureWorkflow> logger,
        IServiceProvider serviceProvider)
    {
        _screenCaptureService = screenCaptureService;
        _ocrService = ocrService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartCaptureWorkflowAsync()
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Hotkey fired.");
        App.LogToFile("Hotkey fired.");
        try
        {
            // Close any existing result window first to prevent screen clutter
            if (_activeResultWindow != null)
            {
                try
                {
                    _activeResultWindow.Close();
                }
                catch
                {
                    // Ignore
                }
                _activeResultWindow = null;
            }

            // 1. Hide the main window to ensure it is not captured in the screenshot
            App.CurrentApp.HideMainWindow();
            App.LogToFile("MainWindow hidden.");

            // Wait a brief moment for the window hide animation/transition to complete
            await Task.Delay(200);

            // 2. Capture the entire virtual screen first
            var fullScreenBytes = await _screenCaptureService.CaptureFullScreenAsync();

            // 3. Show region selection overlay and set the background screenshot
            var selectionWindow = new SelectionWindow();
            _logger.LogInformation("Overlay created.");
            App.LogToFile("Overlay created.");
            await selectionWindow.SetBackgroundAsync(fullScreenBytes);
            selectionWindow.Activate();

            // Force topmost layout and foreground synchronously!
            selectionWindow.ForceForegroundAndTopmost();

            var region = await selectionWindow.GetSelectionAsync();
            if (region == null)
            {
                _logger.LogInformation("Screen capture canceled by user.");
                App.LogToFile("Screen capture canceled by user.");
                return;
            }

            var (x, y, w, h) = region.Value;
            _logger.LogInformation("Region selected at ({X}, {Y}) with size {Width}x{Height}", x, y, w, h);
            App.LogToFile($"Region selected at ({x}, {y}) with size {w}x{h}");

            // 4. Crop the selected region from our screenshot in memory
            var timestamp = DateTime.UtcNow;
            var imageBytes = await _screenCaptureService.CropImageAsync(fullScreenBytes, (int)x, (int)y, (int)w, (int)h);

            // 5. Run OCR using the DI registered services
            string recognizedText = string.Empty;
            long ocrInferenceTimeMs = 0;
            using (var scope = _serviceProvider.CreateScope())
            {
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                string language = settings?.OcrLanguage ?? "Japanese";

                var ocrStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var ocrResult = await _ocrService.RecognizeTextAsync(imageBytes, language);
                ocrStopwatch.Stop();
                ocrInferenceTimeMs = ocrStopwatch.ElapsedMilliseconds;

                recognizedText = ocrResult?.FullText?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(recognizedText))
                {
                    recognizedText = "No text detected.";
                }
            }

            // 6. Display popup result window
            var resultViewModel = new CaptureResultViewModel();
            await resultViewModel.SetCaptureDataAsync(imageBytes, w, h, timestamp, recognizedText);

            _activeResultWindow = new CaptureResultWindow(resultViewModel, x, y, w, h);
            _activeResultWindow.Activate();

            totalStopwatch.Stop();
            _logger.LogInformation("OCR inference time: {OcrTime} ms", ocrInferenceTimeMs);
            _logger.LogInformation("Total workflow time: {TotalTime} ms", totalStopwatch.ElapsedMilliseconds);
            App.LogToFile($"OCR inference time: {ocrInferenceTimeMs} ms | Total workflow time: {totalStopwatch.ElapsedMilliseconds} ms");
            _logger.LogInformation("Capture result window opened.");
            App.LogToFile("Capture result window opened.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during screen capture workflow");
            App.LogToFile($"Error occurred during screen capture workflow: {ex.Message}");
        }
    }
}
