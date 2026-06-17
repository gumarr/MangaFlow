using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Services;

namespace MangaFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IGlossaryService, GlossaryService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<IContextMemoryService, ContextMemoryService>();
        services.AddSingleton<IBubbleMemoryService, BubbleMemoryService>();
        services.AddScoped<ITranslationContextService, TranslationContextService>();
        services.AddScoped<ITranslationService, TranslationService>();
        services.AddScoped<IOcrBenchmarkService, OcrBenchmarkService>();

        return services;
    }
}
