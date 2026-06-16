using Microsoft.Extensions.DependencyInjection;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.OCR;

public static class DependencyInjection
{
    public static IServiceCollection AddOcrServices(this IServiceCollection services)
    {
        services.AddSingleton<IOcrService, OcrServiceStub>();
        return services;
    }
}
