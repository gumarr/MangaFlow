using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface IOcrService
{
    Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language);
    Task<OcrResult> RecognizeTextAsync(string imagePath, string language);
}
