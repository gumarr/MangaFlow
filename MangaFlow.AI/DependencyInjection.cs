using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        // Singleton: model stays loaded for app lifetime, never reload per-request
        services.AddSingleton<LlamaCppTranslationProvider>();
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<LlamaCppTranslationProvider>());

        return services;
    }
}
