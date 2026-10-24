using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SETUNA;
using SETUNA.Main;
using SETUNA.Main.Cache;
using SETUNA.Main.Clothesline;
using SETUNA.Main.Option;
using SETUNA.Main.StyleItems;

namespace SETUNATests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineTests
    {
        private string _originalCachePath;
        private string _tempCacheRoot;

        [TestInitialize]
        public void Setup()
        {
            // 缓存指向临时目录，不触碰真实用户截图
            _originalCachePath = CacheManager.Path;
            _tempCacheRoot = Path.Combine(Path.GetTempPath(), "SETUNATests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempCacheRoot);
            CacheManager.Path = _tempCacheRoot;
        }

        [TestCleanup]
        public void Cleanup()
        {
            CacheManager.Path = _originalCachePath;
            if (Directory.Exists(_tempCacheRoot))
            {
                Directory.Delete(_tempCacheRoot, true);
            }
        }

        /// 按主程序 Mainform.LoadOption/SaveOption 的实际方式序列化/反序列化
        private static SetunaOption RoundTripViaXml(SetunaOption option)
        {
            var allType = SetunaOption.GetAllType();
            using (var stream = new MemoryStream())
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(option.GetType(), allType);
                serializer.Serialize(stream, option);
                stream.Position = 0;
                return (SetunaOption)new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), allType).Deserialize(stream);
            }
        }

        /// CacheItem.FolderPath 的目录名格式（DateTimeExtensions internal，此处用相同格式）
        private static string FolderName(DateTime createTime)
        {
            return createTime.ToString("yyyy-MM-dd HH-mm-ss-ff");
        }

        [TestMethod]
        public void Deserialize_OldXmlWithoutClotheslineFields_UsesDefaults()
        {
            var oldXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<SetunaOption xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Difficult>false</Difficult>
  <ScrapHotKeyEnable>false</ScrapHotKeyEnable>
</SetunaOption>";

            var allType = SetunaOption.GetAllType();
            var option = (SetunaOption)new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), allType)
                .Deserialize(new StringReader(oldXml));

            Assert.IsFalse(option.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.MonitorCenter, option.ClotheslinePreviewLocation);
            Assert.IsFalse(option.ClotheslineRopePositionSaved);
        }

        [TestMethod]
        public void RoundTrip_NonDefaultClotheslineFields_ValuesPreserved()
        {
            var option = new SetunaOption
            {
                ClotheslineEnabled = true,
                ClotheslinePreviewLocation = ClotheslinePreviewLocation.NearThumbnail,
                ClotheslineRopePosition = new Point(123, 456),
                ClotheslineRopePositionSaved = true
            };

            var loaded = RoundTripViaXml(option);

            Assert.IsTrue(loaded.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.NearThumbnail, loaded.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(123, 456), loaded.ClotheslineRopePosition);
            Assert.IsTrue(loaded.ClotheslineRopePositionSaved);
        }

        [TestMethod]
        public void Clone_ModifyOriginalNewFields_CloneIsolated()
        {
            var option = new SetunaOption
            {
                ClotheslineEnabled = true,
                ClotheslinePreviewLocation = ClotheslinePreviewLocation.OriginalLocation,
                ClotheslineRopePosition = new Point(10, 20),
                ClotheslineRopePositionSaved = true
            };

            var clone = (SetunaOption)option.Clone();

            // 初始值一致
            Assert.IsTrue(clone.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.OriginalLocation, clone.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(10, 20), clone.ClotheslineRopePosition);
            Assert.IsTrue(clone.ClotheslineRopePositionSaved);

            // 修改原对象不影响克隆
            option.ClotheslineEnabled = false;
            option.ClotheslinePreviewLocation = ClotheslinePreviewLocation.NearThumbnail;
            option.ClotheslineRopePosition = new Point(99, 99);
            option.ClotheslineRopePositionSaved = false;
            Assert.IsTrue(clone.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.OriginalLocation, clone.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(10, 20), clone.ClotheslineRopePosition);
            Assert.IsTrue(clone.ClotheslineRopePositionSaved);

            // 反向：修改克隆不影响原对象
            clone.ClotheslineEnabled = true;
            clone.ClotheslineRopePosition = new Point(77, 88);
            Assert.IsFalse(option.ClotheslineEnabled);
            Assert.AreEqual(new Point(99, 99), option.ClotheslineRopePosition);
        }

        [TestMethod]
        public void CacheItem_SaveInfoRead_WithTrueFalseNull_Preserved()
        {
            var createTime = new DateTime(2026, 1, 1, 12, 0, 0);
            var item = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = true
            };
            item.SaveInfo();

            var loaded = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsTrue(loaded.ClotheslineHanging.Value);

            // false 保持 false
            var itemFalse = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = false
            };
            itemFalse.SaveInfo();
            var loadedFalse = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsFalse(loadedFalse.ClotheslineHanging.Value);

            // null 保持 null
            var itemNull = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = null
            };
            itemNull.SaveInfo();
            var loadedNull = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsNull(loadedNull.ClotheslineHanging);
        }

        [TestMethod]
        public void CacheItem_ReadOldJsonWithoutClotheslineHanging_IsNull()
        {
            var createTime = new DateTime(2026, 2, 2, 8, 30, 0);
            var folder = Path.Combine(CacheManager.Path, FolderName(createTime));
            Directory.CreateDirectory(folder);
            var oldJson = @"{
  ""CreateTime"": """ + createTime.ToString("yyyy-MM-ddTHH:mm:ss") + @""",
  ""Position"": ""1, 2"",
  ""SortingOrder"": 0
}";
            File.WriteAllText(Path.Combine(folder, "Info.json"), oldJson);

            var loaded = CacheItem.Read(folder);
            Assert.IsNotNull(loaded);
            Assert.IsNull(loaded.ClotheslineHanging);
        }

        /// 暴露 protected 构造函数用于测试
        private class CacheItemAccessForTest : CacheItem
        {
        }
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineLayoutTests
    {
        // —— 预览等比适配 ——

        [TestMethod]
        public void CalculateDisplayRectangle_LandscapeImageWithinWorkArea_KeepsOriginalSize()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculateDisplayRectangle(new Size(800, 600), workArea);

            Assert.AreEqual(800, result.Width);
            Assert.AreEqual(600, result.Height);
        }

        [TestMethod]
        public void CalculateDisplayRectangle_PortraitImageTooTall_ScaledDownKeepingRatio()
        {
            var workArea = new Rectangle(0, 0, 1000, 500);
            var result = ClotheslinePreviewForm.CalculateDisplayRectangle(new Size(500, 1000), workArea);

            // 高度受限：高=500，宽=250
            Assert.AreEqual(500, result.Height);
            Assert.AreEqual(250, result.Width);
        }

        [TestMethod]
        public void CalculateDisplayRectangle_ExtremelyWideImage_ScaledDownKeepingRatio()
        {
            var workArea = new Rectangle(0, 0, 1000, 800);
            var result = ClotheslinePreviewForm.CalculateDisplayRectangle(new Size(4000, 200), workArea);

            // 宽度受限：宽=1000，高=50
            Assert.AreEqual(1000, result.Width);
            Assert.AreEqual(50, result.Height);
            Assert.AreEqual(20.0, 4000 / (double)200, 0.01);
            Assert.AreEqual(20.0, result.Width / (double)result.Height, 0.01);
        }

        [TestMethod]
        public void CalculateDisplayRectangle_NegativeOriginWorkArea_SizeIndependentOfOrigin()
        {
            var workArea = new Rectangle(-1920, -200, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculateDisplayRectangle(new Size(3000, 3000), workArea);

            Assert.AreEqual(1040, result.Height);
            Assert.AreEqual(1040, result.Width);
        }

        [TestMethod]
        public void CalculateDisplayRectangle_ImageLargerThanWorkArea_NeverExceedsWorkArea()
        {
            var workArea = new Rectangle(-500, 100, 640, 480);
            var result = ClotheslinePreviewForm.CalculateDisplayRectangle(new Size(1920, 1080), workArea);

            Assert.IsTrue(result.Width <= workArea.Width);
            Assert.IsTrue(result.Height <= workArea.Height);
            // 等比：16:9
            Assert.AreEqual(16.0 / 9, result.Width / (double)result.Height, 0.02);
        }

        // —— 预览定位夹紧 ——

        [TestMethod]
        public void CalculatePreviewLocation_MonitorCenter_PositiveWorkAreaCentered()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.MonitorCenter, new Size(400, 300), workArea, Point.Empty, Point.Empty);

            Assert.AreEqual(new Point(760, 370), result);
        }

        [TestMethod]
        public void CalculatePreviewLocation_MonitorCenter_NegativeWorkAreaCentered()
        {
            var workArea = new Rectangle(-1920, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.MonitorCenter, new Size(400, 300), workArea, Point.Empty, Point.Empty);

            Assert.AreEqual(new Point(-1920 + 760, 370), result);
        }

        [TestMethod]
        public void CalculatePreviewLocation_NearThumbnail_AnchorOffScreen_ClampedIntoWorkArea()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            // 锚点在屏幕外右下
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.NearThumbnail, new Size(400, 300), workArea, new Point(5000, 5000), Point.Empty);

            Assert.AreEqual(new Point(1920 - 400, 1040 - 300), result);
        }

        [TestMethod]
        public void CalculatePreviewLocation_NearThumbnail_AnchorWithinScreen_CenteredOnAnchor()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.NearThumbnail, new Size(400, 300), workArea, new Point(960, 520), Point.Empty);

            Assert.AreEqual(new Point(960 - 200, 520 - 150), result);
        }

        [TestMethod]
        public void CalculatePreviewLocation_OriginalLocation_InsideWorkArea_UsedAsIs()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.OriginalLocation, new Size(400, 300), workArea, Point.Empty, new Point(100, 200));

            Assert.AreEqual(new Point(100, 200), result);
        }

        [TestMethod]
        public void CalculatePreviewLocation_OriginalLocation_OutsideWorkArea_Clamped()
        {
            var workArea = new Rectangle(0, 0, 1920, 1040);
            var result = ClotheslinePreviewForm.CalculatePreviewLocation(
                ClotheslinePreviewLocation.OriginalLocation, new Size(400, 300), workArea, Point.Empty, new Point(-500, 2000));

            Assert.AreEqual(new Point(0, 1040 - 300), result);
        }

        // —— 绳子缩略图与命中 ——

        [TestMethod]
        public void CalculateThumbnailSize_LandscapeImage_ScaledToWidthLimit()
        {
            var size = ClotheslineForm.CalculateThumbnailSize(new Size(4000, 200));

            Assert.AreEqual(ClotheslineForm.MaxThumbWidth, size.Width);
            Assert.AreEqual(9, size.Height); // 200*180/4000 = 9
        }

        [TestMethod]
        public void CalculateThumbnailSize_SmallImage_NotUpscaled()
        {
            var size = ClotheslineForm.CalculateThumbnailSize(new Size(100, 50));

            Assert.AreEqual(100, size.Width);
            Assert.AreEqual(50, size.Height);
        }

        [TestMethod]
        public void CalculateThumbnailSize_PortraitImage_ScaledToHeightLimit()
        {
            var size = ClotheslineForm.CalculateThumbnailSize(new Size(300, 600));

            Assert.AreEqual(ClotheslineForm.MaxThumbHeight, size.Height);
            Assert.AreEqual(70, size.Width); // 300*140/600 = 70
            Assert.AreEqual(0.5, size.Width / (double)size.Height, 0.01);
        }

        [TestMethod]
        public void FindItemIndexAt_PointOnPhoto_ReturnsIndex()
        {
            var rects = new List<Rectangle> { new Rectangle(16, 32, 100, 80), new Rectangle(128, 32, 100, 80) };

            Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 0, vertical: false));
            Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(200, 50), 0, vertical: false));
        }

        [TestMethod]
        public void FindItemIndexAt_PointOnGapOrClip_ReturnsNegative()
        {
            var rects = new List<Rectangle> { new Rectangle(16, 32, 100, 80) };

            // 间隙（条目右侧）
            Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(120, 50), 0, vertical: false));
            // 夹子（照片上方绳线区域）
            Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(66, 10), 0, vertical: false));
        }

        [TestMethod]
        public void FindItemIndexAt_WithScrollOffset_MapsClientPointCorrectly()
        {
            var rects = new List<Rectangle> { new Rectangle(16, 32, 100, 80), new Rectangle(128, 32, 100, 80) };

            // 滚动偏移 100 后：条目0 移出可视区左侧，客户区 x=50 落在条目1（128-100=28..128）
            Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 100, vertical: false));
            // 条目0 在客户区坐标 -84..16
            Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(0, 50), 100, vertical: false));
        }

        // —— 自动关闭判定 ——

        [TestMethod]
        public void IsPointerOverPreviewSurface_PointOnSourceOrPreview_True()
        {
            var sourceRect = new Rectangle(100, 100, 50, 40);
            var previewRect = new Rectangle(300, 200, 400, 300);

            Assert.IsTrue(ClotheslineController.IsPointerOverPreviewSurface(new Point(120, 120), sourceRect, previewRect));
            Assert.IsTrue(ClotheslineController.IsPointerOverPreviewSurface(new Point(500, 400), sourceRect, previewRect));
        }

        [TestMethod]
        public void IsPointerOverPreviewSurface_PointInGapBetweenRects_False()
        {
            // 两矩形间的间隙（允许跨越间隙意味着间隙内就应关闭）
            var sourceRect = new Rectangle(100, 100, 50, 40);
            var previewRect = new Rectangle(300, 200, 400, 300);

            Assert.IsFalse(ClotheslineController.IsPointerOverPreviewSurface(new Point(200, 150), sourceRect, previewRect));
        }

        [TestMethod]
        public void IsPointerOverPreviewSurface_EmptySourceRect_PreviewOnly()
        {
            var previewRect = new Rectangle(300, 200, 400, 300);

            Assert.IsTrue(ClotheslineController.IsPointerOverPreviewSurface(new Point(400, 300), Rectangle.Empty, previewRect));
            Assert.IsFalse(ClotheslineController.IsPointerOverPreviewSurface(new Point(100, 100), Rectangle.Empty, previewRect));
        }

        // —— 200ms 自动关闭宽限状态机 ——

        [TestMethod]
        public void NextOutsideSince_PointerOverSurface_ResetsOutsideTime()
        {
            // 即使此前已在离开状态，回到矩形内即重置
            Assert.IsNull(ClotheslineController.NextOutsideSince(1000, true, 1100));
        }

        [TestMethod]
        public void NextOutsideSince_FirstTickOutside_RecordsNowNotClose()
        {
            // 首次离开：记录时间，不立即关闭（宽限开始）
            var result = ClotheslineController.NextOutsideSince(null, false, 1000);

            Assert.AreEqual(1000L, result);
        }

        [TestMethod]
        public void NextOutsideSince_StillOutside_KeepsOriginalTime()
        {
            // 持续离开：保留首次离开时间，供调用方比较持续时间
            var result = ClotheslineController.NextOutsideSince(1000, false, 1050);

            Assert.AreEqual(1000L, result);
        }

        [TestMethod]
        public void AutoCloseGraceSequence_CrossGapWithin200ms_ThenReturn_DoesNotClose()
        {
            // 模拟跨间隙：离开 50ms 轮询 3 tick 后回到矩形内 → 离开时间被重置
            long? outside = null;
            outside = ClotheslineController.NextOutsideSince(outside, false, 1000);
            outside = ClotheslineController.NextOutsideSince(outside, false, 1050);
            outside = ClotheslineController.NextOutsideSince(outside, false, 1100);
            Assert.AreEqual(1000L, outside);

            // 回到矩形内 → 重置
            outside = ClotheslineController.NextOutsideSince(outside, true, 1150);
            Assert.IsNull(outside);
        }

        [TestMethod]
        public void AutoCloseGraceSequence_LeftOver200ms_ElapsedTriggersClose()
        {
            // 离开后经过 >= 200ms 的 tick：调用方按 now - outsideSince >= 200 判定关闭
            long? outside = ClotheslineController.NextOutsideSince(null, false, 1000);
            outside = ClotheslineController.NextOutsideSince(outside, false, 1050);
            outside = ClotheslineController.NextOutsideSince(outside, false, 1200);

            // 1200 - 1000 = 200 >= 200 → 应关闭
            Assert.IsTrue(1200 - outside.Value >= 200);
        }

        // —— 绘制/命中滚动偏移一致性 ——

        [TestMethod]
        public void PaintAndHitTest_ScrollOffsetConsistency_SameTransform()
        {
            // 绘制与命中必须用同一换算：rect.X - scrollOffset。
            // 命中：滚动 120 后条目1（原 X=208）的客户区范围 88..188
            var rects = new List<Rectangle> { new Rectangle(16, 32, 180, 140), new Rectangle(208, 32, 180, 140) };
            var hitIndex = ClotheslineForm.FindItemIndexAt(rects, new Point(100, 50), 120, vertical: false);
            Assert.AreEqual(1, hitIndex);

            // 绘制同一矩形应用偏移后的位置 = 命中判定的矩形位置
            var painted = rects[1];
            painted.Offset(-120, 0);
            Assert.AreEqual(88, painted.X);
            Assert.IsTrue(painted.Contains(new Point(100, 50)));
        }
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineLifecycleTests
    {
        private string _originalCachePath;
        private string _tempCacheRoot;

        [TestInitialize]
        public void Setup()
        {
            _originalCachePath = CacheManager.Path;
            _tempCacheRoot = Path.Combine(Path.GetTempPath(), "SETUNATests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempCacheRoot);
            CacheManager.Path = _tempCacheRoot;
        }

        [TestCleanup]
        public void Cleanup()
        {
            CacheManager.Path = _originalCachePath;
            if (Directory.Exists(_tempCacheRoot))
            {
                Directory.Delete(_tempCacheRoot, true);
            }
        }

        private static string FolderName(DateTime createTime)
        {
            return createTime.ToString("yyyy-MM-dd HH-mm-ss-ff");
        }

        /// 在 STA 线程上执行（WinForms 窗体需要 STA）；失败时透传异常
        private static void RunOnSta(Action action)
        {
            var failure = (Exception)null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw failure;
            }
        }

        /// <summary>
        /// 在带完整消息循环的 STA 线程上执行。DrawToBitmap（CompactScrap 构造路径）
        /// 依赖创建线程上的消息处理，裸 STA 线程会死锁，故用 Application.Run 承载。
        /// ScrapBase.OnPaint 解引用 Mainform.Instance.optSetuna，须先提供 Mainform 实例，
        /// 否则首帧绘制 NRE 会让消息循环静默挂死。
        /// </summary>
        private static void RunOnStaWithMessagePump(Action action)
        {
            var failure = (Exception)null;
            var done = new System.Threading.ManualResetEvent(false);
            var thread = new System.Threading.Thread(() =>
            {
                var host = new Form();
                host.Shown += (s, e) =>
                {
                    try
                    {
                        var mainform = new Mainform();
                        action();
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        done.Set();
                        Application.ExitThread();
                    }
                };
                Application.Run(host);
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            done.WaitOne(30000);
            thread.Join(5000);
            if (failure != null)
            {
                throw failure;
            }
        }

        private static CacheItem CreateTempCacheItem(DateTime createTime)
        {
            return new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(30, 40)
            };
        }

        // —— 缓存三态语义（纯逻辑 + 临时缓存文件） ——

        [TestMethod]
        public void ResolveHanging_NullMigratesToTrue_AndWritesBack()
        {
            var createTime = new DateTime(2026, 3, 3, 9, 0, 0);
            var item = CreateTempCacheItem(createTime);
            Assert.IsNull(item.ClotheslineHanging);

            Assert.IsTrue(ClotheslineCacheMigration.ResolveHanging(item, clotheslineEnabled: true));
            Assert.IsTrue(item.ClotheslineHanging.Value);

            var loaded = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsTrue(loaded.ClotheslineHanging.Value);
        }

        [TestMethod]
        public void ResolveHanging_ModeDisabled_DoesNotMigrateOrHang()
        {
            var createTime = new DateTime(2026, 3, 3, 9, 30, 0);
            var item = CreateTempCacheItem(createTime);
            Assert.IsNull(item.ClotheslineHanging);

            Assert.IsFalse(ClotheslineCacheMigration.ResolveHanging(item, clotheslineEnabled: false));
            // 模式关闭不迁移，缓存保持 null（旧缓存语义不丢失）
            Assert.IsNull(item.ClotheslineHanging);
        }

        [TestMethod]
        public void ResolveHanging_FalseStaysFalse_NoRewrite()
        {
            var createTime = new DateTime(2026, 3, 3, 10, 0, 0);
            var item = CreateTempCacheItem(createTime);
            item.ClotheslineHanging = false;
            item.SaveInfo();

            Assert.IsFalse(ClotheslineCacheMigration.ResolveHanging(item, clotheslineEnabled: true));

            var loaded = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsFalse(loaded.ClotheslineHanging.Value);
        }

        [TestMethod]
        public void ResolveHanging_NullItem_ReturnsFalse()
        {
            Assert.IsFalse(ClotheslineCacheMigration.ResolveHanging(null, clotheslineEnabled: true));
        }

        // —— 挂起/取下生命周期（STA 冒烟） ——

        [TestMethod]
        public void HangAndDetach_PositionAndScaleUnchanged()
        {
            RunOnSta(() =>
            {
                var scrap = new ScrapBase();
                try
                {
                    scrap.Image = new Bitmap(200, 100);
                    scrap.Location = new Point(111, 222);
                    scrap.Scale = 150;

                    scrap.SetClotheslineHanging(true);
                    Assert.IsTrue(scrap.IsClotheslineHanging);
                    Assert.AreEqual(new Point(111, 222), scrap.Location);
                    Assert.AreEqual(150, scrap.Scale);

                    scrap.SetClotheslineHanging(false);
                    Assert.IsFalse(scrap.IsClotheslineHanging);
                    Assert.AreEqual(new Point(111, 222), scrap.Location);
                    Assert.AreEqual(150, scrap.Scale);
                }
                finally
                {
                    scrap.ScrapClose();
                }
            });
        }

        [TestMethod]
        public void Hang_SuppressesShowViaSetVisibleCore_DetachRestores()
        {
            RunOnSta(() =>
            {
                var scrap = new ScrapBase();
                try
                {
                    scrap.Show();
                    Assert.IsTrue(scrap.Visible);

                    scrap.SetClotheslineHanging(true);
                    Assert.IsFalse(scrap.Visible);

                    // 挂起期间 Show/Visible 赋值全部被 SetVisibleCore 压制
                    scrap.Show();
                    scrap.Visible = true;
                    Assert.IsFalse(scrap.Visible);

                    scrap.SetClotheslineHanging(false);
                    Assert.IsTrue(scrap.Visible);
                }
                finally
                {
                    scrap.ScrapClose();
                }
            });
        }

        [TestMethod]
        public void Hang_HiddenBeforeStayHiddenAfterDetach()
        {
            RunOnSta(() =>
            {
                var scrap = new ScrapBase();
                try
                {
                    scrap.Image = new Bitmap(200, 100);
                    scrap.Show();
                    scrap.Hide();
                    // 挂起前就是隐藏状态 → 取下后保持隐藏
                    scrap.SetClotheslineHanging(true);
                    scrap.SetClotheslineHanging(false);
                    Assert.IsFalse(scrap.Visible);
                }
                finally
                {
                    scrap.ScrapClose();
                }
            });
        }

        [TestMethod]
        public void Hang_WritesCacheOnlyOnTransition()
        {
            RunOnSta(() =>
            {
                var createTime = DateTime.Now;
                var item = CreateTempCacheItem(createTime);
                item.SaveInfo();

                var scrap = new ScrapBase();
                try
                {
                    scrap.CacheItem = item;
                    scrap.SetClotheslineHanging(true);
                    Assert.IsTrue(item.ClotheslineHanging.Value);

                    var loaded = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
                    Assert.IsTrue(loaded.ClotheslineHanging.Value);

                    scrap.SetClotheslineHanging(false);
                    var loadedAfter = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
                    Assert.IsFalse(loadedAfter.ClotheslineHanging.Value);
                }
                finally
                {
                    scrap.CacheItem = null;
                    scrap.ScrapClose();
                }
            });
        }

        [TestMethod]
        public void SetVisibleCore_RepeatHangCalls_Idempotent()
        {
            RunOnSta(() =>
            {
                var scrap = new ScrapBase();
                try
                {
                    scrap.Show();
                    scrap.SetClotheslineHanging(true);
                    // 重复挂起不改变状态（简报约束 11：重复恢复不重置取下状态的同族检查）
                    scrap.SetClotheslineHanging(true);
                    Assert.IsTrue(scrap.IsClotheslineHanging);

                    scrap.SetClotheslineHanging(false);
                    scrap.SetClotheslineHanging(false);
                    Assert.IsFalse(scrap.IsClotheslineHanging);
                    Assert.IsTrue(scrap.Visible);
                }
                finally
                {
                    scrap.ScrapClose();
                }
            });
        }

        [TestMethod]
        public void Hang_Detach_RaisesHangingChangedEachTransition()
        {
            RunOnSta(() =>
            {
                var scrap = new ScrapBase();
                try
                {
                    var changes = new List<bool>();
                    scrap.ClotheslineHangingChanged += (s, e) => changes.Add(((ScrapBase)s).IsClotheslineHanging);

                    scrap.SetClotheslineHanging(true);
                    scrap.SetClotheslineHanging(true);
                    scrap.SetClotheslineHanging(false);

                    CollectionAssert.AreEqual(new List<bool> { true, false }, changes);
                }
                finally
                {
                    scrap.ScrapClose();
                }
            });
        }

        // —— 收缩样式组合（简报场景 5） ——

        private static CCompactStyleItem CreateCompactItem()
        {
            return new CCompactStyleItem { LineColor = Color.Blue.ToArgb(), SoldLine = false, Opacity = 100 };
        }

        [TestMethod]
        public void HangWithCompactForm_NoVisibleWindowLeak_DetachRestoresCompact()
        {
            RunOnStaWithMessagePump(() =>
            {
                var scrap = new ScrapBase();
                CompactScrap compact = null;
                try
                {
                    scrap.Image = new Bitmap(200, 100);
                    scrap.Show();

                    // 挂起前处于收缩样式：收缩窗可见、原窗被其隐藏
                    compact = new CompactScrap(scrap, CreateCompactItem(), Point.Empty);
                    compact.Show();
                    Assert.IsTrue(compact.Visible);
                    Assert.IsFalse(scrap.Visible);
                    Assert.AreEqual(compact, scrap.StyleForm);

                    scrap.SetClotheslineHanging(true);
                    // 挂起后无可见窗口泄漏
                    Assert.IsFalse(scrap.Visible);
                    Assert.IsFalse(compact.Visible);

                    // 挂起期间所有展示路径被压制
                    scrap.Show();
                    compact.Show();
                    Assert.IsFalse(scrap.Visible);
                    Assert.IsFalse(compact.Visible);

                    scrap.SetClotheslineHanging(false);
                    // 取下恢复挂起前的组合：收缩窗可见、原窗保持隐藏
                    Assert.IsTrue(compact.Visible);
                    Assert.IsFalse(scrap.Visible);
                }
                finally
                {
                    scrap.StyleForm = null;
                    compact?.Dispose();
                    scrap.ScrapClose();
                    scrap.Dispose();
                }
            });
        }

        [TestMethod]
        public void CompactCreatedWhileHanging_StaysHidden()
        {
            RunOnStaWithMessagePump(() =>
            {
                var scrap = new ScrapBase();
                CompactScrap compact = null;
                try
                {
                    scrap.Image = new Bitmap(200, 100);
                    scrap.Show();
                    scrap.SetClotheslineHanging(true);

                    // 模拟挂起中的样式恢复：收缩窗在挂起状态下创建并 Show
                    compact = new CompactScrap(scrap, CreateCompactItem(), Point.Empty);
                    compact.Show();

                    // 初始化完成（StyleForm 绑定、位置换算），但不泄漏可见窗口
                    Assert.AreEqual(compact, scrap.StyleForm);
                    Assert.IsFalse(compact.Visible);
                    Assert.IsFalse(scrap.Visible);

                    // 取下后恢复收缩窗组合
                    scrap.SetClotheslineHanging(false);
                    Assert.IsTrue(compact.Visible);
                }
                finally
                {
                    scrap.StyleForm = null;
                    compact?.Dispose();
                    scrap.ScrapClose();
                    scrap.Dispose();
                }
            });
        }
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineSpaceKeyStateMachineTests
    {
        // —— 简报 9.1：down→up（消费） ——

        [TestMethod]
        public void DownHit_Toggles_ConsumeUp_RoundEnds()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();

            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
            Assert.AreEqual(SpaceKeyAction.Consume, sm.OnKeyUp());
            // 轮次结束，可开始新轮
            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
        }

        [TestMethod]
        public void DownHit_RepeatKeydown_DoesNotToggleAgain_UpConsumed()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();

            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
            // 按住产生的 repeat（低级钩子同为 keydown 消息）：跟随首轮，不重复切换
            Assert.AreEqual(SpaceKeyAction.Consume, sm.OnKeyDown(true));
            Assert.AreEqual(SpaceKeyAction.Consume, sm.OnKeyDown(true));
            Assert.AreEqual(SpaceKeyAction.Consume, sm.OnKeyUp());
        }

        // —— 非命中 down 透传后按住移入：本轮不接管 ——

        [TestMethod]
        public void DownMiss_PassThrough_MoveOverThumbnailDuringHold_DoesNotTakeOver()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();

            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyDown(false));
            // 按住期间鼠标移入缩略图：repeat 仍透传（本轮命中判定已定）
            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyDown(true));
            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyUp());
        }

        // —— 无 down 的 keyup（钩子安装时已按住等） ——

        [TestMethod]
        public void KeyUpWithoutDown_PassThrough_WaitsForNextDown()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();

            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyUp());
            // 之后首个 down 正常开始新一轮
            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
        }

        [TestMethod]
        public void MarkAlreadyDown_WholeRoundPassesThrough_UntilKeyUp()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();
            sm.MarkAlreadyDown();

            // 安装时已按住：本轮 repeat/keyup 全部透传
            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyDown(true));
            Assert.AreEqual(SpaceKeyAction.PassThrough, sm.OnKeyUp());
            // 该轮 keyup 结束后恢复正常
            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
        }

        // —— ConsumingRound 配对（延迟卸钩判定） ——

        [TestMethod]
        public void ConsumingRound_TrueOnlyWhileConsumedDownPending()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();
            Assert.IsFalse(sm.ConsumingRound);

            sm.OnKeyDown(true);
            Assert.IsTrue(sm.ConsumingRound);

            sm.OnKeyUp();
            Assert.IsFalse(sm.ConsumingRound);

            sm.OnKeyDown(false);
            Assert.IsFalse(sm.ConsumingRound);
        }

        [TestMethod]
        public void Reset_ClearsPendingRound()
        {
            var sm = new ClotheslineSpaceKeyStateMachine();
            sm.OnKeyDown(true);
            sm.Reset();

            Assert.IsFalse(sm.ConsumingRound);
            Assert.AreEqual(SpaceKeyAction.TogglePreview, sm.OnKeyDown(true));
        }
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslinePauseStateTests
    {
        // —— 简报 9.2：全部隐藏与捕获暂停互不清除 ——

        [TestMethod]
        public void SinglePause_EnteredExit_ShouldHideTracksCorrectly()
        {
            var state = new ClotheslinePauseState();
            Assert.IsFalse(state.ShouldHide);

            state.SetAllHidden(true);
            Assert.IsTrue(state.ShouldHide);

            state.SetAllHidden(false);
            Assert.IsFalse(state.ShouldHide);
        }

        [TestMethod]
        public void CapturePauseExit_DoesNotClearAllHidden()
        {
            var state = new ClotheslinePauseState();
            state.SetAllHidden(true);
            state.SetCapturePaused(true);

            // 捕获结束只清除捕获暂停，全部隐藏仍生效
            state.SetCapturePaused(false);
            Assert.IsTrue(state.ShouldHide);
            Assert.IsTrue(state.AllHidden);
            Assert.IsFalse(state.CapturePaused);
        }

        [TestMethod]
        public void AllHiddenExit_DoesNotClearCapturePaused()
        {
            var state = new ClotheslinePauseState();
            state.SetAllHidden(true);
            state.SetCapturePaused(true);

            state.SetAllHidden(false);
            Assert.IsTrue(state.ShouldHide);
            Assert.IsFalse(state.AllHidden);
            Assert.IsTrue(state.CapturePaused);
        }

        [TestMethod]
        public void BothPaused_ExitOneAtATime_ShowOnlyAfterBothReleased()
        {
            var state = new ClotheslinePauseState();
            state.SetAllHidden(true);
            state.SetCapturePaused(true);

            state.SetCapturePaused(false);
            Assert.IsTrue(state.ShouldHide);

            state.SetAllHidden(false);
            Assert.IsFalse(state.ShouldHide);
        }

        [TestMethod]
        public void RepeatedSetSameValue_Idempotent()
        {
            var state = new ClotheslinePauseState();
            state.SetAllHidden(true);
            state.SetAllHidden(true);
            state.SetCapturePaused(true);
            state.SetCapturePaused(true);

            Assert.IsTrue(state.ShouldHide);

            state.SetCapturePaused(false);
            state.SetCapturePaused(false);
            Assert.IsTrue(state.ShouldHide);
        }
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineModeActionTests
    {
        // —— 简报 9.3：重复应用设置幂等（OptionApply 决策层） ——

        [TestMethod]
        public void DecideModeAction_SameState_NoAction()
        {
            Assert.AreEqual(ClotheslineModeAction.None, ClotheslineController.DecideModeAction(true, true));
            Assert.AreEqual(ClotheslineModeAction.None, ClotheslineController.DecideModeAction(false, false));
        }

        [TestMethod]
        public void DecideModeAction_StateChanged_EnableOrDisable()
        {
            Assert.AreEqual(ClotheslineModeAction.Enable, ClotheslineController.DecideModeAction(true, false));
            Assert.AreEqual(ClotheslineModeAction.Disable, ClotheslineController.DecideModeAction(false, true));
        }

        [TestMethod]
        public void DecideModeAction_RepeatedApplySequence_StaysNone()
        {
            // 模拟 OptionApply 幂等路径：开→开→开，仅第一次产生 Enable
            var actions = new List<ClotheslineModeAction>();
            var modeActive = false;
            foreach (var enabled in new[] { true, true, true })
            {
                var action = ClotheslineController.DecideModeAction(enabled, modeActive);
                actions.Add(action);
                if (action == ClotheslineModeAction.Enable)
                {
                    modeActive = true;
                }
            }

            CollectionAssert.AreEqual(
                new List<ClotheslineModeAction> { ClotheslineModeAction.Enable, ClotheslineModeAction.None, ClotheslineModeAction.None },
                actions);
        }

        [TestMethod]
        public void DecideModeAction_ToggleOnOffOn_EachTransitionActioned()
        {
            var modeActive = false;
            var actions = new List<ClotheslineModeAction>();
            foreach (var enabled in new[] { true, false, true })
            {
                var action = ClotheslineController.DecideModeAction(enabled, modeActive);
                actions.Add(action);
                modeActive = action != ClotheslineModeAction.None ? enabled : modeActive;
            }

            CollectionAssert.AreEqual(
                new List<ClotheslineModeAction> { ClotheslineModeAction.Enable, ClotheslineModeAction.Disable, ClotheslineModeAction.Enable },
                actions);
        }
    }

    /// 暴露 CacheItem protected 构造函数用于测试
    public class CacheItemAccessForTest : CacheItem
    {
    }

    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineOrientationTests
    {
        [TestMethod]
        public void ResolveVertical_AutoOnLandscapeWorkArea_IsFalse()
        {
            var wa = new Rectangle(0, 0, 1920, 1032);
            Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
        }

        [TestMethod]
        public void ResolveVertical_AutoOnPortraitWorkArea_IsTrue()
        {
            // 负坐标竖屏（旋转显示器）
            var wa = new Rectangle(-1080, 0, 1080, 1920);
            Assert.IsTrue(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
        }

        [TestMethod]
        public void ResolveVertical_ExplicitValues_OverrideWorkArea()
        {
            var wa = new Rectangle(0, 0, 1920, 1032);
            Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Horizontal, wa));
            Assert.IsTrue(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Vertical, wa));
        }

        [TestMethod]
        public void ResolveVertical_AutoSquareWorkArea_IsFalse()
        {
            // 正方形工作区按横（Height > Width 严格判定）
            var wa = new Rectangle(0, 0, 1000, 1000);
            Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
        }

        [TestMethod]
        public void CalculateDefaultRopePosition_Horizontal_MatchesCurrentTopLeft()
        {
            var wa = new Rectangle(0, 0, 1920, 1032);
            var p = ClotheslineController.CalculateDefaultRopePosition(false, wa);
            Assert.AreEqual(new Point(100, 8), p);
        }

        [TestMethod]
        public void CalculateDefaultRopePosition_Vertical_RightEdgeCentered()
        {
            var wa = new Rectangle(0, 0, 1920, 1032);
            var p = ClotheslineController.CalculateDefaultRopePosition(true, wa);
            // 右边缘留 8px 间距，垂直居中
            Assert.AreEqual(wa.Right - 60 - 8, p.X);
            Assert.AreEqual(wa.Y + (wa.Height - 200) / 2, p.Y);
        }

        [TestMethod]
        public void CalculateDefaultRopePosition_Vertical_NegativeOriginWorkArea_StaysInside()
        {
            var wa = new Rectangle(-1080, 0, 1080, 1920);
            var p = ClotheslineController.CalculateDefaultRopePosition(true, wa);
            Assert.IsTrue(wa.Contains(new Rectangle(p, new Size(60, 200))));
        }

        [TestMethod]
        public void SetunaOption_OrientationRoundTrip_PreservesValue()
        {
            var so = new SetunaOption();
            Assert.AreEqual(ClotheslineRopeOrientation.Auto, so.ClotheslineRopeOrientation); // 默认 Auto
            so.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Vertical;
            var clone = (SetunaOption)so.Clone();
            Assert.AreEqual(ClotheslineRopeOrientation.Vertical, clone.ClotheslineRopeOrientation);
            clone.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Horizontal;
            Assert.AreEqual(ClotheslineRopeOrientation.Vertical, so.ClotheslineRopeOrientation); // 隔离
        }

        [TestMethod]
        public void LayoutItemRects_Vertical_StacksTopToBottomLatestLast()
        {
            var items = new List<ClotheslineItem>();
            var bmp1 = new Bitmap(100, 50);   // 横图
            var bmp2 = new Bitmap(50, 100);   // 竖图
            items.Add(new ClotheslineItem(null, bmp1));
            items.Add(new ClotheslineItem(null, bmp2));
            var rects = ClotheslineForm.LayoutItemRects(items, vertical: true);
            Assert.AreEqual(2, rects.Count);
            // 从上往下：第二个条目（最新）在更下方
            Assert.IsTrue(rects[1].Y > rects[0].Y);
            // X 全部对齐（同一列）
            Assert.AreEqual(rects[0].X, rects[1].X);
            bmp1.Dispose(); bmp2.Dispose();
        }

        [TestMethod]
        public void LayoutItemRects_Horizontal_Regression_RowLeftToRight()
        {
            var items = new List<ClotheslineItem>();
            var bmp1 = new Bitmap(100, 50);
            var bmp2 = new Bitmap(100, 50);
            items.Add(new ClotheslineItem(null, bmp1));
            items.Add(new ClotheslineItem(null, bmp2));
            var rects = ClotheslineForm.LayoutItemRects(items, vertical: false);
            Assert.AreEqual(2, rects.Count);
            Assert.IsTrue(rects[1].X > rects[0].X);   // 最新在右
            Assert.AreEqual(rects[0].Y, rects[1].Y);  // 同一行
            bmp1.Dispose(); bmp2.Dispose();
        }

        [TestMethod]
        public void FindItemIndexAt_VerticalAppliesScrollOffset()
        {
            var rects = new List<Rectangle>
            {
                new Rectangle(20, 30, 100, 50),
                new Rectangle(20, 100, 100, 50)
            };
            // 无偏移：命中第一个
            Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 0, vertical: true));
            // Y 偏移 70：条目1（原 Y 100..150）换算后 30..80，点 (50,50) 命中条目1
            Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 70, vertical: true));
            // 间隙不命中（条目0 下方 80..100 之间）
            Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 95), 0, vertical: true));
        }
    }
}
