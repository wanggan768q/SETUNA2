using System;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 暂停状态对（纯逻辑，供测试）：全部隐藏与捕获暂停分别记录，
    /// 单独进入/退出、同时进入/退出的组合互不清除，仅在两者都解除时才放行显示。
    /// </summary>
    public class ClotheslinePauseState
    {
        private bool _allHidden;
        private bool _capturePaused;

        /// <summary>是否应保持隐藏（任一暂停生效中）。</summary>
        public bool ShouldHide => _allHidden || _capturePaused;

        public bool AllHidden => _allHidden;

        public bool CapturePaused => _capturePaused;

        public void SetAllHidden(bool value)
        {
            _allHidden = value;
        }

        public void SetCapturePaused(bool value)
        {
            _capturePaused = value;
        }
    }
}
