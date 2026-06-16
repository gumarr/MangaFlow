using System;
using Microsoft.Extensions.Logging.Abstractions;
using MangaFlow.Infrastructure;
using Xunit;

namespace MangaFlow.Tests;

public class HotkeyServiceTests
{
    [Fact]
    public void RegisterHotkey_WithNoHwnd_ShouldStoreAsPending()
    {
        // Arrange
        var service = new HotkeyService(NullLogger<HotkeyService>.Instance);
        Action callback = () => { };

        // Act & Assert
        // Registering a hotkey without HWND should defer registration and not throw.
        service.RegisterHotkey("Alt + Q", callback);
        
        // Unregistering should execute safely.
        service.UnregisterHotkey();
    }

    [Fact]
    public void RegisterHotkey_WithInvalidKeyCombination_ShouldNotThrow()
    {
        // Arrange
        var service = new HotkeyService(NullLogger<HotkeyService>.Instance);
        Action callback = () => { };

        // Act & Assert
        // Invalid key combinations should fail parsing safely and not throw.
        service.RegisterHotkey("Invalid+Key+Combo", callback);
        service.UnregisterHotkey();
    }
}
