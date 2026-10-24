using System;
using System.Drawing;
using System.IO;
using Avalonia.Media.Imaging;
using GdiBitmap = System.Drawing.Bitmap;
using AvBitmap = Avalonia.Media.Imaging.Bitmap;

namespace SETUNA.Avalonia.Bridging
{
    /// <summary>
    /// GDI Bitmap ↔ Avalonia Bitmap 转换。PNG 流编解码实现（代码量小、语义清晰）；
    /// 截图/贴图路径的像素格式（24bpp/32bpp）都兼容。
    /// </summary>
    public static class GdiBitmapConverter
    {
        /// <summary>GDI Bitmap → Avalonia Bitmap（经 PNG 流）。</summary>
        public static AvBitmap ToAvalonia(GdiBitmap source)
        {
            if (source == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            source.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new AvBitmap(stream);
        }

        /// <summary>Avalonia Bitmap → GDI Bitmap（经 PNG 流）。</summary>
        public static GdiBitmap ToGdi(AvBitmap source)
        {
            if (source == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            source.Save(stream);
            stream.Position = 0;
            return new GdiBitmap(stream);
        }

        /// <summary>字节 → Avalonia Bitmap。</summary>
        public static AvBitmap FromBytes(byte[] data)
        {
            using var stream = new MemoryStream(data);
            return new AvBitmap(stream);
        }
    }
}
