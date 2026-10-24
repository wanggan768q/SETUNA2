using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>剪贴板：直接 P/Invoke CF_DIB/CF_UNICODETEXT，比 WinForms Clipboard 的 OLE 封装行为更可控。</summary>
    public sealed class WindowsClipboardService : IClipboardService
    {
        private const uint CF_DIB = 8;
        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint GMEM_MOVEABLE = 0x0002;

        public void SetImage(Bitmap image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            // GDI+ DIB：32bpp RGB（BGRA），自上而下需要负 biHeight
            var rect = new Rectangle(0, 0, image.Width, image.Height);
            var data = image.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                // BITMAPINFOHEADER (40) + 像素
                var pixelBytes = data.Stride * data.Height;
                var total = 40 + pixelBytes;
                var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)total);
                if (hMem == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("GlobalAlloc 失败");
                }

                var ptr = GlobalLock(hMem);
                try
                {
                    // BITMAPINFOHEADER
                    Marshal.WriteInt32(ptr, 0, 40);              // biSize
                    Marshal.WriteInt32(ptr, 4, image.Width);     // biWidth
                    Marshal.WriteInt32(ptr, 8, -image.Height);   // biHeight（负=自上而下）
                    Marshal.WriteInt16(ptr, 12, 1);              // biPlanes
                    Marshal.WriteInt16(ptr, 14, 32);             // biBitCount
                    Marshal.WriteInt32(ptr, 16, 0);              // biCompression = BI_RGB
                    Marshal.WriteInt32(ptr, 20, pixelBytes);     // biSizeImage
                    // 其余字段 0

                    // 像素数据：GDI+ stride 4 字节对齐，32bpp DIB stride 恒为 width*4，两者一致
                    CopyPixels(data.Scan0, pixelBytes, ptr + 40);
                }
                finally
                {
                    GlobalUnlock(hMem);
                }

                if (!OpenClipboard(IntPtr.Zero))
                {
                    GlobalFree(hMem);
                    throw new InvalidOperationException("OpenClipboard 失败");
                }
                try
                {
                    EmptyClipboard();
                    if (SetClipboardData(CF_DIB, hMem) == IntPtr.Zero)
                    {
                        GlobalFree(hMem); // SetClipboardData 成功后系统接管内存，失败才需释放
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            finally
            {
                image.UnlockBits(data);
            }
        }

        public Bitmap GetImage()
        {
            if (!IsClipboardFormatAvailable(CF_DIB))
            {
                return null;
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                return null;
            }
            try
            {
                var hData = GetClipboardData(CF_DIB);
                if (hData == IntPtr.Zero)
                {
                    return null;
                }
                var ptr = GlobalLock(hData);
                try
                {
                    var size = (int)GlobalSize(hData);
                    if (size < 40)
                    {
                        return null;
                    }

                    var width = Marshal.ReadInt32(ptr, 4);
                    var heightRaw = Marshal.ReadInt32(ptr, 8);
                    var bitCount = Marshal.ReadInt16(ptr, 14);
                    var compression = Marshal.ReadInt32(ptr, 16);

                    if (bitCount != 32 || compression != 0 || width <= 0)
                    {
                        // 仅支持 32bpp BI_RGB；其他格式走 GDI+ 兜底
                        return GetImageViaGdiPlus();
                    }
                    var bottomUp = heightRaw > 0;
                    var height = Math.Abs(heightRaw);

                    var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var dest = bitmap.LockBits(new Rectangle(0, 0, width, height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        unsafe
                        {
                            var src = ptr + 40;
                            var srcStride = width * 4;
                            for (var y = 0; y < height; y++)
                            {
                                // DIB 行序：bottom-up 时首行是图像最下行
                                var srcRow = bottomUp ? (height - 1 - y) : y;
                                Buffer.MemoryCopy((void*)(src + srcRow * srcStride),
                                    (void*)dest.Scan0, dest.Stride, Math.Min(srcStride, dest.Stride));
                            }
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(dest);
                    }
                    return bitmap;
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static unsafe void CopyPixels(IntPtr src, int bytes, IntPtr dest)
        {
            Buffer.MemoryCopy((void*)src, (void*)dest, bytes, bytes);
        }

        private static Bitmap GetImageViaGdiPlus()
        {
            // 旧工程其他调用点用 WinForms Clipboard；此处兜底走 DataObject 复制语义
            try
            {
                var dataObject = System.Windows.Forms.Clipboard.GetDataObject();
                return dataObject?.GetData(System.Windows.Forms.DataFormats.Bitmap) as Bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void SetText(string text)
        {
            System.Windows.Forms.Clipboard.SetText(text ?? string.Empty);
        }

        public string GetText()
        {
            return System.Windows.Forms.Clipboard.ContainsText()
                ? System.Windows.Forms.Clipboard.GetText()
                : null;
        }

        public bool ContainsImage => IsClipboardFormatAvailable(CF_DIB) || System.Windows.Forms.Clipboard.ContainsImage();

        public bool ContainsText => System.Windows.Forms.Clipboard.ContainsText();
    }
}
