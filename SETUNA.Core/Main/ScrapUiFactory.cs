using System;
using System.Drawing;

namespace SETUNA.Main
{
    /// <summary>
    /// 贴图窗体工厂：Core 的 ScrapBook 需要创建贴图实体（AddScrap/缓存恢复/哑贴图），
    /// 具体窗体由宿主注册。未注册时抛异常（Avalonia 宿主必须在启动时注册）。
    /// </summary>
    public static class ScrapUiFactory
    {
        /// <summary>创建空白贴图实体（原 new ScrapBase()）。</summary>
        public static Func<IScrap> CreateScrap;

        /// <summary>样式事件监听者（原 Mainform 作为 IScrapStyleListener）。</summary>
        public static Func<IScrapStyleListener> StyleListener;

        /// <summary>菜单事件监听者（原 Mainform 作为 IScrapMenuListener）。</summary>
        public static Func<IScrapMenuListener> MenuListener;

        /// <summary>创建并填充 IScrap；工厂未注册时抛异常。</summary>
        public static IScrap NewScrap()
        {
            if (CreateScrap == null)
            {
                throw new InvalidOperationException("ScrapUiFactory.CreateScrap 未注册。宿主启动时须注册贴图工厂。");
            }
            return CreateScrap();
        }
    }
}
