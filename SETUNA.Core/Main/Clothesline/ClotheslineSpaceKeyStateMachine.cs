using System;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 空格键处理结果：透传给后续应用 / 吞掉 / 吞掉并切换预览。
    /// </summary>
    public enum SpaceKeyAction
    {
        PassThrough,
        Consume,
        TogglePreview
    }

    /// <summary>
    /// 空格键轮次状态机（纯逻辑，供测试）：首个 keydown 消息决定本轮是否消费
    /// （命中才消费并只切换一次预览），重复 keydown（按住产生的 repeat，低级钩子
    /// 与首次 down 同为 WM_KEYDOWN）跟随本轮决定不重复切换；keyup 按首轮决定消费
    /// 后轮次结束。非命中 down 透传后整轮不接管（按住移入缩略图不中途生效）。
    /// </summary>
    public class ClotheslineSpaceKeyStateMachine
    {
        private bool _downSeen;
        private bool _downConsumed;

        /// <summary>是否正处已消费 down、等待配对 keyup 的轮次（卸钩配对判定）。</summary>
        public bool ConsumingRound => _downSeen && _downConsumed;

        /// <summary>本轮是否已见 down（含未命中轮次；keyup 收尾判定用）。</summary>
        public bool RoundActive => _downSeen;

        public SpaceKeyAction OnKeyDown(bool hit)
        {
            if (!_downSeen)
            {
                _downSeen = true;
                _downConsumed = hit;
                return hit ? SpaceKeyAction.TogglePreview : SpaceKeyAction.PassThrough;
            }

            // 本轮的重复 keydown：跟随首轮决定，不重复切换
            return _downConsumed ? SpaceKeyAction.Consume : SpaceKeyAction.PassThrough;
        }

        public SpaceKeyAction OnKeyUp()
        {
            if (!_downSeen)
            {
                // 无 down 的 keyup（钩子安装时已按住等场景）：不消费，等下一个 down 开始
                return SpaceKeyAction.PassThrough;
            }

            var result = _downConsumed ? SpaceKeyAction.Consume : SpaceKeyAction.PassThrough;
            Reset();
            return result;
        }

        /// <summary>钩子安装时物理键已按住：本轮无 down 可见，整轮透传直到该轮 keyup 结束。</summary>
        public void MarkAlreadyDown()
        {
            _downSeen = true;
            _downConsumed = false;
        }

        public void Reset()
        {
            _downSeen = false;
            _downConsumed = false;
        }
    }
}
