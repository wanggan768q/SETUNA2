using System;
using System.Collections.Generic;
using System.Drawing;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// 屏幕捕获：BitBlt 实现（对齐原 CaptureForm.CopyFromScreen，SRCCOPY|CAPTUREBLT），
    /// 显示器信息走 System.Windows.Forms.Screen（本程序集为 net9.0-windows）。
    /// </summary>
    public sealed class WindowsScreenCaptureService : IScreenCaptureService
    {
        public Bitmap CaptureScreen(Rectangle bounds)
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var screenDc = IntPtr.Zero;
            var destDc = IntPtr.Zero;
            bool success = false;
            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    destDc = graphics.GetHdc();
                    // 0x40CC0020 = SRCCOPY | CAPTUREBLT，与原实现字面量一致
                    success = NativeMethods.BitBlt(destDc, 0, 0, bounds.Width, bounds.Height,
                        screenDc, bounds.X, bounds.Y, NativeMethods.SRCCOPY_CAPTURBLT) != 0;
                }
            }
            finally
            {
                // destDc 的 ReleaseHdc 由 using（Graphics.Dispose）配对兜底
                if (screenDc != IntPtr.Zero)
                {
                    NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }

            if (!success)
            {
                bitmap.Dispose();
                throw new InvalidOperationException($"BitBlt 截屏失败 bounds={bounds}");
            }
            return bitmap;
        }

        /// <summary>抓屏诊断：返回左上 64x64 区域的平均亮度（0-255）。全黑=0 附近。</summary>
        public static int ProbeBrightness(Bitmap snapshot)
        {
            try
            {
                long sum = 0;
                var n = 0;
                for (var y = 0; y < Math.Min(64, snapshot.Height); y += 4)
                {
                    for (var x = 0; x < Math.Min(64, snapshot.Width); x += 4)
                    {
                        var c = snapshot.GetPixel(x, y);
                        sum += (c.R + c.G + c.B) / 3;
                        n++;
                    }
                }
                return n > 0 ? (int)(sum / n) : -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProbeBrightness failed: {ex.Message}");
                return -1;
            }
        }

        public IReadOnlyList<MonitorInfo> GetMonitors()
        {
            var result = new List<MonitorInfo>();
            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                result.Add(FromScreen(screen));
            }
            return result;
        }

        public Rectangle GetVirtualScreenBounds()
        {
            return System.Windows.Forms.SystemInformation.VirtualScreen;
        }

        public MonitorInfo GetMonitorFromPoint(Point point)
        {
            return FromScreen(System.Windows.Forms.Screen.FromPoint(point));
        }

        public Point GetCursorPosition()
        {
            return System.Windows.Forms.Cursor.Position;
        }

        public void DrawCursorToImage(Bitmap target, Point targetLocation)
        {
            var screenDc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                using (var graphics = Graphics.FromImage(target))
                {
                    var hdc = graphics.GetHdc();
                    try
                    {
                        NativeMethods.DrawCursorImageToScreenImage(targetLocation, hdc);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }
                }
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        internal static MonitorInfo FromScreen(System.Windows.Forms.Screen screen)
        {
            return new MonitorInfo
            {
                Bounds = screen.Bounds,
                WorkingArea = screen.WorkingArea,
                // WinForms Screen 不直接暴露 DPI；1.0 作为回退，Avalonia 端有准确值
                Scaling = 1.0,
                IsPrimary = screen.Primary,
                DisplayName = screen.DeviceName,
            };
        }
    }
}
