using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App.Views;

public sealed partial class CaptureResultWindow : Window
{
    public CaptureResultViewModel ViewModel { get; }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public CaptureResultWindow(CaptureResultViewModel viewModel, double? x = null, double? y = null, double? w = null, double? h = null)
    {
        this.InitializeComponent();
        ViewModel = viewModel;

        // Configure window presenter to stay on top and be completely borderless/frameless
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Get window handle and monitor DPI scale factor
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        uint dpi = 96;
        try
        {
            dpi = GetDpiForWindow(hwnd);
        }
        catch
        {
            // Fallback to standard 96 DPI if API call fails
        }
        double scaleFactor = dpi / 96.0;

        // Base unscaled target sizes (prioritizing width over height to reduce wrapping/scrolling)
        int charCount = viewModel.RecognizedText?.Length ?? 0;
        double unscaledWidth = 400;
        double unscaledHeight = 120;

        if (charCount < 100)
        {
            // Short text: compact tooltip
            unscaledWidth = 400;
            unscaledHeight = 120;
        }
        else if (charCount < 400)
        {
            // Medium text: wider tooltip to avoid wrapping, low height
            unscaledWidth = 650;
            unscaledHeight = 180;
        }
        else if (charCount < 800)
        {
            // Long text: expand width significantly, keeping height minimal
            unscaledWidth = 900;
            unscaledHeight = 280;
        }
        else
        {
            // Very long text: expand to max preferred bounds, introducing scrollbar only for extremely large translations
            unscaledWidth = 1100;
            unscaledHeight = 350;

            if (charCount > 1500)
            {
                unscaledHeight = 550;
            }
        }

        // Apply DPI scaling to sizes
        int targetWidth = (int)(unscaledWidth * scaleFactor);
        int targetHeight = (int)(unscaledHeight * scaleFactor);

        // Clamping bounds (also DPI scaled)
        int minWidthScaled = (int)(400 * scaleFactor);
        int preferredWidthScaled = (int)(650 * scaleFactor);
        int maxWidthScaled = (int)(1200 * scaleFactor);

        int minHeightScaled = (int)(120 * scaleFactor);
        int preferredHeightScaled = (int)(350 * scaleFactor);
        int maxHeightScaled = (int)(900 * scaleFactor);

        targetWidth = Math.Clamp(targetWidth, minWidthScaled, maxWidthScaled);
        targetHeight = Math.Clamp(targetHeight, minHeightScaled, maxHeightScaled);

        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));

        // Position tooltip near the capture region with multi-level fallback
        if (x.HasValue && y.HasValue && w.HasValue && h.HasValue)
        {
            // Get virtual screen bounds for positioning boundary checks
            int screenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int screenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            // Default placement: Right of selection, aligned with Top
            int posX = (int)(x.Value + w.Value + 10);
            int posY = (int)y.Value;

            // Fallback 1: If it exceeds the right edge, move to the Left side of selection
            if (posX + targetWidth > screenLeft + screenWidth)
            {
                posX = (int)(x.Value - targetWidth - 10);

                // Fallback 2: If it also exceeds the left edge, place it Above the selection
                if (posX < screenLeft)
                {
                    posX = (int)x.Value;
                    posY = (int)(y.Value - targetHeight - 10);

                    // Fallback 3: If it also exceeds the top edge, place it Below the selection
                    if (posY < screenTop)
                    {
                        posY = (int)(y.Value + h.Value + 10);
                    }
                }
            }

            // Final safety clamp to guarantee the tooltip is fully visible within the monitor area
            posX = Math.Clamp(posX, screenLeft + 10, screenLeft + screenWidth - targetWidth - 10);
            posY = Math.Clamp(posY, screenTop + 10, screenTop + screenHeight - targetHeight - 10);

            this.AppWindow.Move(new Windows.Graphics.PointInt32(posX, posY));
        }

        // Add debug logging
        App.LogToFile($"[Tooltip] Monitor DPI: {dpi} (Scale: {scaleFactor:F2}) | Text Length: {charCount} | Calculated Size: {targetWidth}x{targetHeight}");

        // Close tooltip when deactivated (click outside)
        this.Activated += (sender, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                this.Close();
            }
        };

        // Setup hooks for Escape and Ctrl+C Copy
        if (this.Content is FrameworkElement root)
        {
            root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    this.Close();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.C)
                {
                    var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                    bool isCtrlDown = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                    if (isCtrlDown)
                    {
                        try
                        {
                            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                            package.SetText(OcrTextBlock.Text);
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                        }
                        catch { }
                        e.Handled = true;
                    }
                }
            }), true);

            // Focus the root content on load so user can copy/type/press escape immediately
            root.Loaded += (s, e) =>
            {
                root.Focus(FocusState.Programmatic);
            };
        }
    }
}
