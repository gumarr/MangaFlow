using System.Threading.Tasks;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.AI;

public class TranslationEngineStub : ITranslationEngine
{
    public bool IsModelLoaded { get; private set; }

    public Task InitializeAsync(string modelPath, int cpuThreads, bool useGpu, double temperature)
    {
        IsModelLoaded = true;
        return Task.CompletedTask;
    }

    public Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string? glossaryContext = null, string? historyContext = null)
    {
        // Return a mock translated text
        return Task.FromResult($"[Stub Translation from {sourceLanguage} to {targetLanguage}]: {text}");
    }

    public Task UnloadModelAsync()
    {
        IsModelLoaded = false;
        return Task.CompletedTask;
    }
}
