namespace DictateForWindows.Core.Models;

public class ScreenCaptureResult
{
    public bool IsSuccess { get; set; }
    public string? ImagePath { get; set; }
    public string? Error { get; set; }

    public static ScreenCaptureResult Success(string imagePath) => new()
    {
        IsSuccess = true,
        ImagePath = imagePath
    };

    public static ScreenCaptureResult Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}
