using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface ITranslationProvider
{
    string Name { get; }
    Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, TranslationContext context);
}
