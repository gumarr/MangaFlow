using System;
using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(Guid projectId, string text, string sourceLanguage, string targetLanguage, string sourceImageHash = "");
}
