using System;
using System.Threading.Tasks;

namespace MangaFlow.Application.Interfaces;

public interface ITranslationService
{
    Task<string> TranslateAsync(Guid projectId, string text, string sourceLanguage, string targetLanguage);
}
