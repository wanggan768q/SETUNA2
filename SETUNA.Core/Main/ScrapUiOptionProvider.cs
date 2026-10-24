using System.Collections.Generic;
using SETUNA.Main.Style;

namespace SETUNA.Main
{
    /// <summary>
    /// 贴图右键菜单的样式表来源：宿主（Mainform / Avalonia 宿主）在配置加载后注册。
    /// </summary>
    public static class ScrapUiOptionProvider
    {
        /// <summary>用户样式列表（原 Mainform.optSetuna.Styles）。</summary>
        public static IReadOnlyList<CStyle> Styles;

        /// <summary>左键点击样式 ID（原 SetunaOption.Scrap.WClickStyleID；0=无）。</summary>
        public static int WClickStyleID;
    }
}
