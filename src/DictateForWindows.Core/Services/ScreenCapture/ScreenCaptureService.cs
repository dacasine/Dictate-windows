using System.Runtime.InteropServices;
using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.ScreenCapture;

public class ScreenCaptureService : IScreenCaptureService
{
    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "DictateForWindows");

    public Task<ScreenCaptureResult> CaptureFullScreenAsync()
    {
        try
        {
            Directory.CreateDirectory(TempFolder);

            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);

            var hdcScreen = GetDC(IntPtr.Zero);
            var hdcMem = CreateCompatibleDC(hdcScreen);
            var hBitmap = CreateCompatibleBitmap(hdcScreen, screenWidth, screenHeight);
            var hOld = SelectObject(hdcMem, hBitmap);

            BitBlt(hdcMem, 0, 0, screenWidth, screenHeight, hdcScreen, 0, 0, SRCCOPY);

            SelectObject(hdcMem, hOld);

            // Save bitmap to file using GDI+
            var imagePath = Path.Combine(TempFolder, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.bmp");
            SaveHBitmapToFile(hBitmap, imagePath, screenWidth, screenHeight);

            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);

            return Task.FromResult(ScreenCaptureResult.Success(imagePath));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ScreenCaptureResult.Failure(ex.Message));
        }
    }

    private static void SaveHBitmapToFile(IntPtr hBitmap, string filePath, int width, int height)
    {
        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
                biSizeImage = (uint)(width * height * 4)
            }
        };

        var pixels = new byte[width * height * 4];
        var hdcScreen = GetDC(IntPtr.Zero);
        GetDIBits(hdcScreen, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        // Write BMP file
        using var fs = File.Create(filePath);
        using var bw = new BinaryWriter(fs);

        // BMP header
        int fileSize = 54 + pixels.Length;
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0); // reserved
        bw.Write(54); // offset to pixel data

        // DIB header
        bw.Write(40); // header size
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1); // planes
        bw.Write((short)32); // bits per pixel
        bw.Write(0); // compression
        bw.Write(pixels.Length); // image size
        bw.Write(0); // x pixels per meter
        bw.Write(0); // y pixels per meter
        bw.Write(0); // colors used
        bw.Write(0); // important colors

        // Pixel data (need to flip vertically since BMP is bottom-up)
        int stride = width * 4;
        for (int y = height - 1; y >= 0; y--)
        {
            bw.Write(pixels, y * stride, stride);
        }
    }

    #region P/Invoke

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    #endregion
}
