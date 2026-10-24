using System;
using Avalonia;

namespace SETUNA.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 平台服务装配：必须在任何 UI / 业务代码触达平台能力之前
            using var platform = new SETUNA.Platform.Windows.WindowsPlatformService();
            SETUNA.Core.Platform.PlatformServices.Register(platform);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
