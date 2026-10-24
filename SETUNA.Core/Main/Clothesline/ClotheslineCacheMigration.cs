using System;
using SETUNA.Main.Cache;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 晾绳挂起缓存三态语义（纯逻辑，供测试）：null（旧缓存）视为挂起并写回迁移结果；
    /// true/false 保持并按需持久化。
    /// </summary>
    public static class ClotheslineCacheMigration
    {
        /// <summary>解析缓存挂起状态并完成 null 迁移写回。clotheslineEnabled 为 false 时不迁移不挂起，维持原展示行为。</summary>
        public static bool ResolveHanging(CacheItem cacheItem, bool clotheslineEnabled)
        {
            if (cacheItem == null || !clotheslineEnabled)
            {
                return false;
            }

            if (cacheItem.ClotheslineHanging == null)
            {
                // 旧缓存未迁移：视为挂起并保存迁移结果
                cacheItem.ClotheslineHanging = true;
                cacheItem.SaveInfo();
                return true;
            }

            return cacheItem.ClotheslineHanging.Value;
        }
    }
}
