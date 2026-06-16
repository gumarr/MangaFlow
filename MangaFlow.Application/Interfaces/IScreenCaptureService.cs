using System.Threading.Tasks;

namespace MangaFlow.Application.Interfaces;

public interface IScreenCaptureService
{
    Task<byte[]> CaptureScreenRegionAsync(double x, double y, double width, double height);
    Task<byte[]> CaptureFullScreenAsync();
    Task<byte[]> CropImageAsync(byte[] imageBytes, int x, int y, int width, int height);
}
