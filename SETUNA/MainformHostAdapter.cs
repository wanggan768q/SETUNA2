using System.Windows.Forms;
using SETUNA.Main.Option;

namespace SETUNA.Main
{
    /// <summary>
    /// 旧 WinForms 宿主（Mainform）到 IScrapBookHost 的适配器。
    /// 留在主程序集：仅旧宿主使用；Avalonia 宿主自行实现 IScrapBookHost。
    /// </summary>
    public sealed class MainformHostAdapter : Cache.ScrapBookAccessor.IScrapBookHost
    {
        private readonly Mainform _mainform;

        public MainformHostAdapter(Mainform mainform)
        {
            _mainform = mainform;
        }

        public SetunaOption Option => _mainform.optSetuna;

        public void AddImageList(ScrapSource src)
        {
            _mainform.AddImageList(src);
        }
    }
}
