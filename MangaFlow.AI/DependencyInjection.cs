using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddSingleton<ITranslationEngine, TranslationEngineStub>();
        return services;
    }
}
