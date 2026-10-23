using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SETUNA.Main.Cache;
using SETUNA.Main.Clothesline;
using SETUNA.Main.Option;

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

            Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 0));
            Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(200, 50), 0));
        }

        [TestMethod]
        public void FindItemIndexAt_PointOnGapOrClip_ReturnsNegative()
        {
            var rects = new List<Rectangle> { new Rectangle(16, 32, 100, 80) };

            // 间隙（条目右侧）
            Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(120, 50), 0));
            // 夹子（照片上方绳线区域）
            Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(66, 10), 0));
        }

        [TestMethod]
        public void FindItemIndexAt_WithScrollOffset_MapsClientPointCorrectly()
        {
            var rects = new List<Rectangle> { new Rectangle(16, 32, 100, 80), new Rectangle(128, 32, 100, 80) };

            // 滚动偏移 100 后：条目0 移出可视区左侧，客户区 x=50 落在条目1（128-100=28..128）
            Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 100));
            // 条目0 在客户区坐标 -84..16
            Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(0, 50), 100));
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
            var hitIndex = ClotheslineForm.FindItemIndexAt(rects, new Point(100, 50), 120);
            Assert.AreEqual(1, hitIndex);

            // 绘制同一矩形应用偏移后的位置 = 命中判定的矩形位置
            var painted = rects[1];
            painted.Offset(-120, 0);
            Assert.AreEqual(88, painted.X);
            Assert.IsTrue(painted.Contains(new Point(100, 50)));
        }
    }
}
