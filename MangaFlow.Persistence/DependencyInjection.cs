using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Persistence;
using MangaFlow.Persistence.Repositories;

namespace MangaFlow.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MangaFlowDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IGlossaryRepository, GlossaryRepository>();
        services.AddScoped<ITranslationHistoryRepository, TranslationHistoryRepository>();
        services.AddScoped<ITranslationMemoryRepository, TranslationMemoryRepository>();
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();

        return services;
    }
}
