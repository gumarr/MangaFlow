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

            // 5. Run OCR and Translation using the DI registered services
            string recognizedText = string.Empty;
            string translatedText = string.Empty;
            long ocrInferenceTimeMs = 0;
            long translationInferenceTimeMs = 0;

            using (var scope = _serviceProvider.CreateScope())
            {
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
                var translationService = scope.ServiceProvider.GetRequiredService<ITranslationService>();

                var settings = await settingsService.GetSettingsAsync();
                string language = settings?.OcrLanguage ?? "English";

                var ocrStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var ocrResult = await _ocrService.RecognizeTextAsync(imageBytes, language);
                ocrStopwatch.Stop();
                ocrInferenceTimeMs = ocrStopwatch.ElapsedMilliseconds;

                recognizedText = ocrResult?.FullText?.Trim() ?? string.Empty;
                // Reflowed text: wrapped bubble lines joined into coherent sentences for the LLM.
                string textForTranslation = ocrResult?.MergedText?.Trim() ?? recognizedText;

                if (!string.IsNullOrEmpty(textForTranslation))
                {
                    // Find active project or use a default one
                    var projects = await projectService.GetAllProjectsAsync();
                    var activeProject = projects.FirstOrDefault();
                    Guid projectId = activeProject?.Id ?? Guid.Empty;
                    string sourceLang = activeProject?.SourceLanguage ?? "English";
                    string targetLang = activeProject?.TargetLanguage ?? "Vietnamese";

                    // Compute source image hash
                    string imageHash = string.Empty;
                    try
                    {
                        using var sha255 = System.Security.Cryptography.SHA256.Create();
                        var hashBytes = sha255.ComputeHash(imageBytes);
                        imageHash = Convert.ToHexString(hashBytes);
                    }
                    catch
                    {
                        imageHash = Guid.NewGuid().ToString("N");
                    }

                    var translationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var translationResult = await translationService.TranslateAsync(
                        projectId,
                        textForTranslation,
                        sourceLang,
                        targetLang,
                        imageHash);
                    translationStopwatch.Stop();
                    translationInferenceTimeMs = translationStopwatch.ElapsedMilliseconds;

                    translatedText = translationResult?.TranslatedText ?? string.Empty;
                }
                else
                {
                    recognizedText = "No text detected.";
                    translatedText = "No text to translate.";
                }
            }

            // Auto-copy Vietnamese translation to clipboard
            if (!string.IsNullOrWhiteSpace(translatedText) && translatedText != "No text to translate.")
            {
                try
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(translatedText);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    App.LogToFile("Translation auto-copied to clipboard.");
                }
                catch (Exception clipEx)
                {
                    _logger.LogWarning(clipEx, "Failed to auto-copy translation to clipboard");
                }
            }

            // 6. Display popup result window
            var resultViewModel = new CaptureResultViewModel();
            await resultViewModel.SetCaptureDataAsync(imageBytes, w, h, timestamp, recognizedText, translatedText);

            _activeResultWindow = new CaptureResultWindow(resultViewModel, x, y, w, h);
            _activeResultWindow.Activate();

            totalStopwatch.Stop();
            _logger.LogInformation("OCR time: {OcrTime} ms | Translation time: {TranslationTime} ms", ocrInferenceTimeMs, translationInferenceTimeMs);
            _logger.LogInformation("Total workflow time: {TotalTime} ms", totalStopwatch.ElapsedMilliseconds);
            App.LogToFile($"OCR time: {ocrInferenceTimeMs} ms | Translation time: {translationInferenceTimeMs} ms | Total workflow time: {totalStopwatch.ElapsedMilliseconds} ms");
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
