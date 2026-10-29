namespace SETUNA.Main.Style
{
    // Token: 0x0200004B RID: 75
    public class CShowVersionStyle : CPreStyle
    {
        // Token: 0x060002C2 RID: 706 RVA: 0x0000F584 File Offset: 0x0000D784
        public CShowVersionStyle()
        {
            _styleid = -5;
            _stylename = "版本信息";
        }

        // Token: 0x060002C3 RID: 707 RVA: 0x0000F5A0 File Offset: 0x0000D7A0
        public override void Apply(ref IScrap scrap)
        {
            // 版本信息弹窗由宿主 UI 工厂提供（WinForms=SplashForm；Avalonia 宿主自行实现）
            StyleUiFactory.ShowSplash();
        }
    }
}
