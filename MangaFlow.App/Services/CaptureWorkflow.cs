using System;
using System.Threading.Tasks;
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
        _logger.LogInformation("Global hotkey triggered. Starting screen capture workflow...");
        try
        {
            // 1. Show region selection overlay
            var selectionWindow = new SelectionWindow();
            selectionWindow.Activate();

            var region = await selectionWindow.GetSelectionAsync();
            if (region == null)
            {
                _logger.LogInformation("Screen capture canceled by user.");
                return;
            }

            var (x, y, w, h) = region.Value;
            _logger.LogInformation("Region selected at ({X}, {Y}) with size {Width}x{Height}", x, y, w, h);

            // 2. Capture the region
            var timestamp = DateTime.UtcNow;
            var imageBytes = await _screenCaptureService.CaptureScreenRegionAsync(x, y, w, h);

            // 3. Display popup result window
            // Create ViewModel manually or resolve it
            var resultViewModel = new CaptureResultViewModel();
            await resultViewModel.SetCaptureDataAsync(imageBytes, w, h, timestamp);

            var resultWindow = new CaptureResultWindow(resultViewModel);
            resultWindow.Activate();
            _logger.LogInformation("Capture result window opened.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during screen capture workflow");
        }
    }
}
