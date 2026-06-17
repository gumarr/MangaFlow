using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface IOcrService
{
    Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language);
    Task<OcrResult> RecognizeTextAsync(string imagePath, string language);

    /// <summary>
    /// Validates OCR model availability. Returns null if ready, or a human-readable
    /// error message (e.g. "OCR model not installed.") if files are missing/misconfigured.
    /// </summary>
    Task<string?> ValidateAsync();

    /// <summary>
    /// Pre-initializes the OCR engine (loads models into memory).
    /// </summary>
    Task InitializeAsync();
}
