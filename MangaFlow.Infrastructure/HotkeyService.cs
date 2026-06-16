using System;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Infrastructure;

public class HotkeyService : IHotkeyService
{
    public void RegisterHotkey(string keyCombination, Action callback)
    {
        // Stub: do nothing or log registration
    }

    public void UnregisterHotkey()
    {
        // Stub: do nothing
    }
}
