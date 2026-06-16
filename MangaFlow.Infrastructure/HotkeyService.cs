using System;
using System.Runtime.InteropServices;
using Windows.System;
using MangaFlow.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MangaFlow.Infrastructure;

public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly ILogger<HotkeyService> _logger;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private IntPtr _hwnd = IntPtr.Zero;
    private Action? _callback;
    private readonly SubclassProc _subclassProcInstance;

    private string? _pendingKeyCombination;
    private Action? _pendingCallback;

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
        _subclassProcInstance = NewSubclassProc;
    }

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _logger.LogInformation("Initializing HotkeyService with HWND: {Hwnd}", hwnd);
        
        bool subclassResult = SetWindowSubclass(_hwnd, _subclassProcInstance, (IntPtr)HOTKEY_ID, IntPtr.Zero);
        _logger.LogInformation("SetWindowSubclass result: {Result}", subclassResult);

        if (_pendingKeyCombination != null && _pendingCallback != null)
        {
            RegisterHotkey(_pendingKeyCombination, _pendingCallback);
            _pendingKeyCombination = null;
            _pendingCallback = null;
        }
    }

    public void RegisterHotkey(string keyCombination, Action callback)
    {
        if (_hwnd == IntPtr.Zero)
        {
            _logger.LogInformation("No HWND available. Deferring hotkey registration for: {KeyCombo}", keyCombination);
            _pendingKeyCombination = keyCombination;
            _pendingCallback = callback;
            return;
        }

        UnregisterHotkey();

        _callback = callback;
        var (modifiers, key) = ParseKeyCombination(keyCombination);
        if (key != 0)
        {
            bool success = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, key);
            _logger.LogInformation("RegisterHotkey for '{KeyCombo}' (modifiers: {Mod}, key: {Key}) result: {Success}", 
                keyCombination, modifiers, key, success);
        }
        else
        {
            _logger.LogWarning("Failed to parse key combination: {KeyCombo}", keyCombination);
        }
    }

    public void UnregisterHotkey()
    {
        if (_hwnd != IntPtr.Zero)
        {
            bool success = UnregisterHotKey(_hwnd, HOTKEY_ID);
            _logger.LogInformation("UnregisterHotkey result: {Success}", success);
        }
        _callback = null;
    }

    private IntPtr NewSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _logger.LogInformation("WM_HOTKEY message intercepted via subclass.");
            _callback?.Invoke();
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private (uint modifiers, uint key) ParseKeyCombination(string keyCombination)
    {
        uint modifiers = 0;
        uint key = 0;

        var parts = keyCombination.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLowerInvariant();
            if (trimmed == "alt")
            {
                modifiers |= MOD_ALT;
            }
            else if (trimmed == "ctrl" || trimmed == "control")
            {
                modifiers |= MOD_CONTROL;
            }
            else if (trimmed == "shift")
            {
                modifiers |= MOD_SHIFT;
            }
            else if (trimmed == "win" || trimmed == "windows")
            {
                modifiers |= MOD_WIN;
            }
            else
            {
                if (Enum.TryParse<VirtualKey>(trimmed, true, out var virtualKey))
                {
                    key = (uint)virtualKey;
                }
                else if (trimmed.Length == 1)
                {
                    char c = trimmed[0];
                    if (c >= 'a' && c <= 'z')
                    {
                        key = (uint)(c - 'a' + VirtualKey.A);
                    }
                    else if (c >= '0' && c <= '9')
                    {
                        key = (uint)(c - '0' + VirtualKey.Number0);
                    }
                }
            }
        }

        return (modifiers, key);
    }

    public void Dispose()
    {
        UnregisterHotkey();
        if (_hwnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hwnd, _subclassProcInstance, (IntPtr)HOTKEY_ID);
        }
        GC.SuppressFinalize(this);
    }
}
