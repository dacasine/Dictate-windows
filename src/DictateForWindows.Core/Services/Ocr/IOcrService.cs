using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.Ocr;

public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(string imagePath);
    IReadOnlyList<string> GetAvailableLanguages();
}
