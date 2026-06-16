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
    private readonly ILogger<CaptureWorkflow> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CaptureResultWindow? _activeResultWindow;
    private static int _testCounter = 0;

    public CaptureWorkflow(
        IScreenCaptureService screenCaptureService,
        ILogger<CaptureWorkflow> logger,
        IServiceProvider serviceProvider)
    {
        _screenCaptureService = screenCaptureService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartCaptureWorkflowAsync()
    {
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
            bool showPreview = false;
            using (var scope = _serviceProvider.CreateScope())
            {
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                showPreview = settings?.ShowCapturePreview ?? false;

                // Mock OCR/Translation text selector (cycles through 50, 300, and 1000 characters)
                int currentCase = _testCounter++ % 3;
                if (currentCase == 0)
                {
                    recognizedText = "Mock OCR Text: Short bubble translation (50 chars).";
                }
                else if (currentCase == 1)
                {
                    recognizedText = "MangaFlow Translation Tooltip UX v1 Mock OCR Text.\n\n" +
                                     "This is a demonstration of the auto-sizing, floating, and topmost WinUI 3 tooltip. " +
                                     "It displays recognized text from the selected screen region. " +
                                     "You can copy this text using Ctrl+C or text selection. Thank you for using MangaFlow!";
                }
                else
                {
                    recognizedText = "MangaFlow Tooltip Scaling and DPI-Awareness Test.\n\n" +
                                     "This is a long mock translation designed to test the tooltip vertical scrollbar and text wrapping behavior. " +
                                     "When the text is very long, we prioritize expanding the width of the tooltip to match wider paragraphs, " +
                                     "which helps reduce the vertical height and prevents excessive wrapping. This mimics real-world manga " +
                                     "translation layouts where text should be comfortably readable without the user needing to constantly scroll.\n\n" +
                                     "Here is some additional text to fill up space and reach the 1000 character target. Reading text in bubbles on " +
                                     "high-resolution displays (such as 1440p, 3K, or 4K monitors) requires adequate DPI scaling, clean typography, " +
                                     "comfortable line spacing, and spacious internal padding. By dynamically scaling the width and height, this " +
                                     "translation tooltip offers a premium reading experience that integrates seamlessly into the background. " +
                                     "Let's make sure that everything stays within the viewport boundaries and is fully selectable. Enjoy reading!";
                }
            }

            // 6. Display popup result window
            var resultViewModel = new CaptureResultViewModel();
            await resultViewModel.SetCaptureDataAsync(imageBytes, w, h, timestamp, recognizedText);

            _activeResultWindow = new CaptureResultWindow(resultViewModel, x, y, w, h);
            _activeResultWindow.Activate();
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
