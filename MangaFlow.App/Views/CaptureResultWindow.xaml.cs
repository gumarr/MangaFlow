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

        // Calculate auto-sizing based on text content length
        int charCount = viewModel.RecognizedText?.Length ?? 0;
        int targetWidth = 350;
        int targetHeight = 120;

        if (charCount < 50)
        {
            targetWidth = 200;
            targetHeight = 60;
        }
        else if (charCount < 150)
        {
            targetWidth = 300;
            targetHeight = 90;
        }
        else if (charCount < 300)
        {
            targetWidth = 400;
            targetHeight = 150;
        }
        else if (charCount < 600)
        {
            targetWidth = 550;
            targetHeight = 250;
        }
        else
        {
            targetWidth = 750;
            targetHeight = 400;
        }

        // Clamp to exact specifications
        targetWidth = Math.Clamp(targetWidth, 200, 800);
        targetHeight = Math.Clamp(targetHeight, 60, 600);

        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));

        // Position tooltip near the capture region
        if (x.HasValue && y.HasValue && w.HasValue && h.HasValue)
        {
            // Position: X = SelectionRect.Right + 10, Y = SelectionRect.Top
            int posX = (int)(x.Value + w.Value + 10);
            int posY = (int)y.Value;

            // Get virtual screen bounds for clamping and fallback detection
            int screenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int screenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            // Fallback: If tooltip would go off-screen on the right, reposition to the left side
            if (posX + targetWidth > screenLeft + screenWidth)
            {
                posX = (int)(x.Value - targetWidth - 10);
            }

            // Clamp X and Y coordinates to make sure it stays inside screen boundaries
            posX = Math.Clamp(posX, screenLeft + 10, screenLeft + screenWidth - targetWidth - 10);
            posY = Math.Clamp(posY, screenTop + 10, screenTop + screenHeight - targetHeight - 10);

            this.AppWindow.Move(new Windows.Graphics.PointInt32(posX, posY));
        }

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
                            package.SetText(OcrTextBox.Text);
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                        }
                        catch { }
                        e.Handled = true;
                    }
                }
            }), true);

            // Focus the TextBox on load so user can copy/type/press escape immediately
            root.Loaded += (s, e) =>
            {
                OcrTextBox.Focus(FocusState.Programmatic);
            };
        }
    }
}
