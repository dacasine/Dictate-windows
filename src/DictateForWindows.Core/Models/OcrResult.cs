namespace DictateForWindows.Core.Models;

public class OcrResult
{
    public bool IsSuccess { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Error { get; set; }
    public double Confidence { get; set; }

    public static OcrResult Success(string text, double confidence = 1.0) => new()
    {
        IsSuccess = true,
        Text = text,
        Confidence = confidence
    };

    public static OcrResult Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}
