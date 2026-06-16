using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MangaFlow.App.Views;

public sealed partial class SelectionWindow : Window
{
    private Point _startPoint;
    private bool _isDragging;
    private readonly TaskCompletionSource<(double X, double Y, double Width, double Height)?> _tcs = new();
    private readonly ILogger<SelectionWindow> _logger;

    // Win32 API Imports for activation & topmost behavior
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr(hWnd, nIndex);
        else
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x0008;

    // GetSystemMetrics indexes
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public SelectionWindow()
    {
        this.InitializeComponent();

        _logger = App.CurrentApp.ServiceProvider.GetRequiredService<ILogger<SelectionWindow>>();

        // Configure borderless window
        var appWindow = this.AppWindow;
        appWindow.IsShownInSwitchers = false;

        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Size to virtual screen
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));

        this.Activated += (s, e) =>
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _logger.LogInformation("Overlay activated. HWND: {Hwnd}", hwnd);
            RootGrid.Focus(FocusState.Programmatic);
        };
    }

    public void ForceForegroundAndTopmost()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _logger.LogInformation("ForceForegroundAndTopmost called on SelectionWindow. HWND: {Hwnd}", hwnd);

        try
        {
            // Force topmost layout via Win32 SetWindowPos
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);

            // Force foreground activation using the thread input attachment pattern
            IntPtr foregroundHwnd = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
            uint currentThreadId = GetCurrentThreadId();

            if (foregroundThreadId != currentThreadId && foregroundThreadId != 0)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
            else
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }

            // Verify and log status
            IntPtr activeHwnd = GetForegroundWindow();
            bool isForeground = (activeHwnd == hwnd);

            IntPtr exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            bool isTopmost = (exStyle.ToInt64() & WS_EX_TOPMOST) != 0;

            _logger.LogInformation("Overlay foreground status: {IsForeground}", isForeground);
            _logger.LogInformation("Overlay topmost status: {IsTopmost}", isTopmost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply topmost/foreground activation to Overlay");
        }
    }

    public Task<(double X, double Y, double Width, double Height)?> GetSelectionAsync()
    {
        return _tcs.Task;
    }

    public async Task SetBackgroundAsync(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0) return;

        try
        {
            var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            {
                using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync();
                }
                stream.Seek(0);
                await bitmapImage.SetSourceAsync(stream);
            }
            BackgroundScreenImage.Source = bitmapImage;
        }
        catch (Exception)
        {
            // Fallback
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(RootGrid);
        if (properties.Properties.IsLeftButtonPressed)
        {
            _startPoint = properties.Position;
            _isDragging = true;
            RootGrid.CapturePointer(e.Pointer);

            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Visibility = Visibility.Visible;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var currentPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
            SelectionRect.Visibility = Visibility.Collapsed;

            var currentPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            if (width > 5 && height > 5)
            {
                double scale = 1.0;
                if (this.Content?.XamlRoot != null)
                {
                    scale = this.Content.XamlRoot.RasterizationScale;
                }
                _tcs.TrySetResult((x * scale, y * scale, width * scale, height * scale));
            }
            else
            {
                _tcs.TrySetResult(null);
            }

            this.Close();
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _tcs.TrySetResult(null);
            this.Close();
        }
    }
}

public class CursorGrid : Grid
{
    public CursorGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    }
}
