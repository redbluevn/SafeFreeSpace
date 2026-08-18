Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class WindowHelper
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

$process = Get-Process SafeFreeSpace -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Output 'SafeFreeSpace process not found'
    exit 1
}

$hwnd = $process.MainWindowHandle
[WindowHelper]::ShowWindow($hwnd, 9)  # SW_RESTORE
[WindowHelper]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

Add-Type -AssemblyName System.Drawing
$bounds = $process.MainWindowHandle
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class ScreenCapture
{
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public static Bitmap CaptureWindow(IntPtr hWnd)
    {
        RECT rect;
        GetWindowRect(hWnd, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        Bitmap bitmap = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }
        return bitmap;
    }
}
'@

$bitmap = [ScreenCapture]::CaptureWindow($process.MainWindowHandle)
$bitmap.Save('D:/LapTrinhAI/SafeDelete/safe-screen.png', [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Output 'Saved safe-screen.png'
