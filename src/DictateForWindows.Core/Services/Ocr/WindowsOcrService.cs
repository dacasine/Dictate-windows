using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace DictateForWindows.Core.Services.Ocr;

public class WindowsOcrService : IOcrService
{
    public async Task<Models.OcrResult> ExtractTextAsync(string imagePath)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null)
            {
                return Models.OcrResult.Failure("No OCR engine available for installed languages");
            }

            var result = await engine.RecognizeAsync(bitmap);
            var text = result.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                return Models.OcrResult.Failure("No text found in image");
            }

            // Estimate confidence based on whether we got words
            var wordCount = result.Lines.Sum(l => l.Words.Count);
            var confidence = wordCount > 0 ? Math.Min(1.0, wordCount / 10.0) : 0.0;

            return Models.OcrResult.Success(text, confidence);
        }
        catch (Exception ex)
        {
            return Models.OcrResult.Failure(ex.Message);
        }
    }

    public IReadOnlyList<string> GetAvailableLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages
            .Select(l => l.DisplayName)
            .ToList();
    }
}
