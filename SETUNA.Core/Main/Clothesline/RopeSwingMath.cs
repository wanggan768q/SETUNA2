using System;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 晾绳摆动数学（纯函数，供测试）：图片绕夹子接触点的简谐摆动与振幅逼近。
    /// 振幅单位度；新挂条目以 6° 起摆、靠逼近自然衰减回 1.2°（约 1 秒落定）。
    /// </summary>
    public static class RopeSwingMath
    {
        /// <summary>闲置振幅（度）。</summary>
        public const double IdleAmplitudeDeg = 1.2;

        /// <summary>悬停振幅（度）。</summary>
        public const double HoverAmplitudeDeg = 4.0;

        /// <summary>新挂条目起摆振幅（度）。</summary>
        public const double NewItemAmplitudeDeg = 6.0;

        /// <summary>摆动周期（秒）。</summary>
        public const double SwingPeriodSeconds = 3.2;

        /// <summary>条目间相位错开（弧度）。</summary>
        public const double PhaseStepRad = 0.7;

        /// <summary>每 tick 振幅逼近步长（度；16ms tick × 0.08 ≈ 5°/秒）。</summary>
        public const double ApproachStepDeg = 0.08;

        /// <summary>摆角（度）：amp × sin(2π·t/周期 + index×相位步)。</summary>
        public static double SwingAngle(double amplitudeDeg, double elapsedSeconds, int itemIndex)
        {
            return amplitudeDeg * Math.Sin(2 * Math.PI * elapsedSeconds / SwingPeriodSeconds + itemIndex * PhaseStepRad);
        }

        /// <summary>振幅向目标每 tick 线性逼近一步，到达即等于目标。</summary>
        public static double ApproachAmplitude(double currentDeg, double targetDeg, double stepDeg)
        {
            var diff = targetDeg - currentDeg;
            if (Math.Abs(diff) <= stepDeg)
            {
                return targetDeg;
            }
            return currentDeg + Math.Sign(diff) * stepDeg;
        }
    }
}
