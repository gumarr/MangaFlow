using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.OCR;

public class OcrServiceStub : IOcrService
{
    public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language)
    {
        return Task.FromResult(CreateMockResult(language));
    }

    public Task<OcrResult> RecognizeTextAsync(string imagePath, string language)
    {
        return Task.FromResult(CreateMockResult(language));
    }

    private OcrResult CreateMockResult(string language)
    {
        var result = new OcrResult();
        
        // Return dummy text block
        result.Blocks.Add(new OcrBlock
        {
            Text = language.Equals("Japanese", StringComparison.OrdinalIgnoreCase) 
                ? "これはモックOCRテキストです。" 
                : "This is mock OCR text.",
            Confidence = 0.95f,
            Box = new BoundingBox(10, 10, 200, 50)
        });

        return result;
    }
}
