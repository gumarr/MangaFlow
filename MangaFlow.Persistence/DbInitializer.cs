using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaFlow.Persistence;

public static class DbInitializer
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MangaFlowDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MangaFlowDbContext>>();
        
        try
        {
            logger.LogInformation("Initializing SQLite database...");
            context.Database.EnsureCreated();
            logger.LogInformation("Database initialized successfully (EnsureCreated).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }
}
