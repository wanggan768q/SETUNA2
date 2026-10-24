namespace SETUNA.Main.Style
{
    // Token: 0x0200004A RID: 74
    public class CCaptureStyle : CPreStyle
    {
        // Token: 0x060002C0 RID: 704 RVA: 0x0000F549 File Offset: 0x0000D764
        public CCaptureStyle()
        {
            _styleid = -9;
            _stylename = "制作参考图";
        }

        // Token: 0x060002C1 RID: 705 RVA: 0x0000F564 File Offset: 0x0000D7A4
        public override void Apply(ref IScrap scrap)
        {
            // 截图触发由宿主命令工厂接管（WinForms=Mainform.StartCapture）
            HostCommands.RequestCapture?.Invoke();
        }
    }
}
