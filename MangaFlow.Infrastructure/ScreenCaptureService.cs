using System.Threading.Tasks;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Infrastructure;

public class ScreenCaptureService : IScreenCaptureService
{
    public Task<byte[]> CaptureScreenRegionAsync(double x, double y, double width, double height)
    {
        // Stub returns empty byte array
        return Task.FromResult(Array.Empty<byte>());
    }
}
