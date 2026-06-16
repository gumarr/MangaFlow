using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        return services;
    }
}
