using System;

namespace SETUNA.Core.Platform
{
    /// <summary>热键 ID（与 SETUNA.Main.HotKeyID 对齐，避免 Core 接口依赖业务命名空间）。</summary>
    public enum HotkeyId
    {
        Capture = 0,
        Function1,
    }

    /// <summary>平台服务静态门面。现有代码风格是 static 单例（LayerManager/CacheManager 同），保持一致。</summary>
    public static class PlatformServices
    {
        private static IPlatformService _instance;

        /// <summary>当前平台服务。必须在使用前由入口装配；未装配时访问抛异常。</summary>
        public static IPlatformService Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("PlatformServices.Instance 未装配。请在应用程序入口调用 Register()。");
                }
                return _instance;
            }
        }

        public static bool IsRegistered => _instance != null;

        public static void Register(IPlatformService service)
        {
            _instance = service ?? throw new ArgumentNullException(nameof(service));
        }
    }
}
