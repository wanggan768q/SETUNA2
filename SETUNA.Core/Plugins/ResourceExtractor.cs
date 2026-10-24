using System;

namespace SETUNA.Plugins
{
    public static class ResourceExtractor
    {
        static bool isExtractWebP = false;

        public static void ExtractWebP()
        {
            if (isExtractWebP)
            {
                return;
            }
            isExtractWebP = true;

            // 资源根命名空间随程序集（SETUNA.Core），双前缀兼容旧 SETUNA 主程序
            var corePrefix = $"{nameof(SETUNA)}.{nameof(Core)}.{nameof(Plugins)}";
            var mainPrefix = $"{nameof(SETUNA)}.{nameof(Plugins)}";
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string ResolveName(string file)
            {
                var core = $"{corePrefix}.{file}";
                if (assembly.GetManifestResourceInfo(core) != null)
                {
                    return core;
                }
                return $"{mainPrefix}.{file}";
            }

            switch (IntPtr.Size)
            {
                case 4:
                    ResourceExtractor.ExtractResourceToFile(ResolveName("libwebp_x86.dll"), "libwebp_x86.dll");
                    break;
                case 8:
                    ResourceExtractor.ExtractResourceToFile(ResolveName("libwebp_x64.dll"), "libwebp_x64.dll");
                    break;
            }
        }


        public static void ExtractResourceToFile(string resourceName, string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                using (var fs = new System.IO.FileStream(filename, System.IO.FileMode.Create))
                {
                    var b = new byte[s.Length];
                    int totalRead = 0;
                    while (totalRead < b.Length)
                    {
                        int bytesRead = s.Read(b, totalRead, b.Length - totalRead);
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }
                    fs.Write(b, 0, totalRead);
                }
            }
        }
    }
}
