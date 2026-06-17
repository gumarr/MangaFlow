using System;
using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface ITranslationContextService
{
    Task<TranslationContext> GetTranslationContextAsync(Guid projectId, string text);
}
