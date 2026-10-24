using System;
using System.Collections.Generic;
using System.IO;
using SETUNA.Main;

namespace SETUNA.Main.Cache
{
    /// <summary>宿主向 Core 回调的能力入口：由 UI 宿主（Mainform）注册实现。</summary>
    public static class ScrapBookAccessor
    {
        private static Func<IScrapBookHost> _hostResolver;

        /// <summary>宿主能力抽象（替代原 ScrapBook → Mainform 直连）。</summary>
        public interface IScrapBookHost
        {
            /// <summary>原 Mainform.optSetuna</summary>
            SETUNA.Main.Option.SetunaOption Option { get; }

            /// <summary>原 Mainform.AddImageList：投递一个待处理图片源（延迟创建贴图）</summary>
            void AddImageList(ScrapSource src);
        }

        private static Func<ScrapBook> _resolver;

        public static void Register(Func<ScrapBook> resolver)
        {
            _resolver = resolver;
        }

        public static void RegisterHost(IScrapBookHost host)
        {
            _hostResolver = () => host;
        }

        internal static Func<IScrapBookHost> HostResolver => _hostResolver;

        public static ScrapBook ResolveBook()
        {
            if (_resolver == null)
            {
                throw new InvalidOperationException("ScrapBookAccessor 未注册。宿主启动时须调用 Register()。");
            }
            return _resolver();
        }

        public static IScrapBookHost ResolveHost()
        {
            if (_hostResolver == null)
            {
                throw new InvalidOperationException("ScrapBookAccessor 未注册宿主。宿主启动时须调用 RegisterHost()。");
            }
            return _hostResolver();
        }
    }

    public class CacheManager : IScrapAddedListener, IScrapRemovedListener, IScrapLocationChangedListener, IScrapImageChangedListener, IScrapStyleAppliedListener, IScrapStyleRemovedListener
    {
        // 缓存根目录（测试可重设指向临时目录）
        public static string Path = System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SETUNA");

        public static readonly CacheManager Instance = new CacheManager();


        public bool IsInit { private set; get; }


        public void Init()
        {
            Init(ScrapBookAccessor.ResolveBook());
        }

        /// <summary>宿主注入 ScrapBook 的初始化（供测试与不同 UI 宿主复用）。</summary>
        public void Init(ScrapBook scrapBook)
        {
            IsInit = false;
            scrapBook.addScrapAddedListener(this);
            scrapBook.addScrapRemovedListener(this);

            RestoreScraps(scrapBook);
        }

        public void DeInit()
        {
        }

        internal void RestoreScraps(ScrapBook mainBook)
        {
            var directoryInfo = new DirectoryInfo(Path);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            var directories = directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
            var list = new List<CacheItem>(directories.Length);
            foreach (var directory in directories)
            {
                var item = CacheItem.Read(directory.FullName);
                if ((item?.IsValid ?? false) == false)
                {
                    continue;
                }

                list.Add(item);
            }

            list.Sort((x, y) => x.SortingOrder.CompareTo(y.SortingOrder));
            AddScrap(0);



            void AddScrap(int index)
            {
                if (index >= list.Count)
                {
                    IsInit = true;
                    return;
                }

                var item = list[index];
                mainBook.AddScrapFromCache(item, () =>
                {
                    AddScrap(++index);
                });
            }
        }


        void IScrapAddedListener.ScrapAdded(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;

            // 已经绑定缓存则忽略
            if (scrap.CacheItem != null)
            {
                return;
            }

            var style = new Style
            {
                ID = scrap.StyleID,
                ClickPoint = scrap.StyleClickPoint
            };

            var cacheItem = CacheItem.Create(scrap.DateTime, scrap.Image, scrap.Location, style);
            scrap.CacheItem = cacheItem;
        }

        void IScrapRemovedListener.ScrapRemoved(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            var cacheItem = scrap?.CacheItem;
            if (cacheItem == null)
            {
                return;
            }

            scrap.CacheItem = null;
            cacheItem.Delete();
        }

        void IScrapLocationChangedListener.ScrapLocationChanged(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            var cacheItem = scrap?.CacheItem;
            if (cacheItem == null)
            {
                return;
            }

            cacheItem.Position = scrap.Location;
            cacheItem.SaveInfo();
        }

        void IScrapImageChangedListener.ScrapImageChanged(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            var cacheItem = scrap?.CacheItem;
            var image = scrap?.Image;
            if (cacheItem == null || image == null)
            {
                return;
            }

            cacheItem.SaveImage(image);
        }

        void IScrapStyleAppliedListener.ScrapStyleApplied(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            var styleID = scrap?.StyleID ?? 0;
            var cacheItem = scrap?.CacheItem;
            if (cacheItem == null || styleID == 0)
            {
                return;
            }

            cacheItem.Style = new Style(styleID, scrap.StyleClickPoint);
            cacheItem.SaveInfo();
        }

        void IScrapStyleRemovedListener.ScrapStyleRemoved(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            var cacheItem = scrap?.CacheItem;
            if (cacheItem == null)
            {
                return;
            }

            cacheItem.Style = new Style(0, new System.Drawing.Point(0, 0));
            cacheItem.SaveInfo();
        }
    }
}