using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaFlow.App.Services;

public class SystemTrayService : IDisposable
{
    private readonly ILogger<SystemTrayService> _logger;
    private IntPtr _hwnd = IntPtr.Zero;
    private DispatcherQueue? _dispatcherQueue;
    private Action? _showAction;
    private Action? _captureAction;
    private Action? _exitAction;
    private bool _isInitialized = false;

    // Win32 API Constants
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int WM_USER = 0x0400;
    private const int WM_TRAYMOUSEMESSAGE = WM_USER + 1024;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private const int TRAY_ICON_ID = 9001;

    // Subclass definition
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, [In] ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly SubclassProc _subclassProcInstance;

    public SystemTrayService(ILogger<SystemTrayService> logger)
    {
        _logger = logger;
        _subclassProcInstance = NewSubclassProc;
    }

    public void Initialize(
        IntPtr hwnd,
        DispatcherQueue dispatcherQueue,
        Action showAction,
        Action captureAction,
        Action exitAction)
    {
        _hwnd = hwnd;
        _dispatcherQueue = dispatcherQueue;
        _showAction = showAction;
        _captureAction = captureAction;
        _exitAction = exitAction;

        _logger.LogInformation("Initializing Native SystemTrayService with HWND: {Hwnd}", hwnd);

        try
        {
            // Set up subclass to listen to tray mouse events
            bool subclassResult = SetWindowSubclass(_hwnd, _subclassProcInstance, (IntPtr)TRAY_ICON_ID, IntPtr.Zero);
            _logger.LogInformation("SystemTray subclass registration result: {Result}", subclassResult);

            // Load icon
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            IntPtr hIcon = IntPtr.Zero;
            if (File.Exists(iconPath))
            {
                hIcon = LoadImage(IntPtr.Zero, iconPath, 1, 16, 16, 0x00000010); // IMAGE_ICON, LR_LOADFROMFILE
            }

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TRAY_ICON_ID,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYMOUSEMESSAGE,
                hIcon = hIcon,
                szTip = "MangaFlow"
            };

            bool success = Shell_NotifyIcon(NIM_ADD, ref data);
            _logger.LogInformation("Shell_NotifyIcon NIM_ADD result: {Success}", success);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize native system tray icon.");
        }
    }

    private IntPtr NewSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_TRAYMOUSEMESSAGE && wParam.ToInt32() == TRAY_ICON_ID)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                _dispatcherQueue?.TryEnqueue(() => _showAction?.Invoke());
                return IntPtr.Zero;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                _dispatcherQueue?.TryEnqueue(() => ShowContextMenu());
                return IntPtr.Zero;
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        try
        {
            var mainWindow = App.CurrentApp.MainWindowInstance;
            if (mainWindow == null) return;

            var content = mainWindow.Content as FrameworkElement;
            if (content == null) return;

            // Get current mouse cursor position
            if (!GetCursorPos(out var ptScreen)) return;

            // Convert screen coordinates to client coordinates of MainWindow
            var ptClient = ptScreen;
            ScreenToClient(_hwnd, ref ptClient);

            // Account for WinUI DPI / Rasterization scale
            double scale = 1.0;
            if (content.XamlRoot != null)
            {
                scale = content.XamlRoot.RasterizationScale;
            }

            var localPoint = new Windows.Foundation.Point(ptClient.X / scale, ptClient.Y / scale);

            // Create standard WinUI MenuFlyout
            var flyout = new MenuFlyout();

            var showItem = new MenuFlyoutItem { Text = "Show / Hide" };
            showItem.Click += (s, e) => _showAction?.Invoke();

            var captureItem = new MenuFlyoutItem { Text = "Capture Screen" };
            captureItem.Click += (s, e) => _captureAction?.Invoke();

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += (s, e) => _exitAction?.Invoke();

            flyout.Items.Add(showItem);
            flyout.Items.Add(captureItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(exitItem);

            // Show the flyout at the cursor position
            flyout.ShowAt(content, localPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to display system tray context menu.");
        }
    }

    public void Dispose()
    {
        if (_isInitialized && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TRAY_ICON_ID
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            RemoveWindowSubclass(_hwnd, _subclassProcInstance, (IntPtr)TRAY_ICON_ID);
            _isInitialized = false;
            _logger.LogInformation("Native SystemTrayService disposed and icon deleted.");
        }
        GC.SuppressFinalize(this);
    }
}
