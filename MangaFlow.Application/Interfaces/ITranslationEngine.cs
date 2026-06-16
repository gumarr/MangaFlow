using System.Threading.Tasks;

namespace MangaFlow.Application.Interfaces;

public interface ITranslationEngine
{
    bool IsModelLoaded { get; }
    Task InitializeAsync(string modelPath, int cpuThreads, bool useGpu, double temperature);
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string? glossaryContext = null, string? historyContext = null);
    Task UnloadModelAsync();
}
