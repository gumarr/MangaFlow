using System;

namespace MangaFlow.Application.Interfaces;

public interface IHotkeyService
{
    void RegisterHotkey(string keyCombination, Action callback);
    void UnregisterHotkey();
}
