using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.ScreenCapture;

public interface IScreenCaptureService
{
    Task<ScreenCaptureResult> CaptureFullScreenAsync();
}
