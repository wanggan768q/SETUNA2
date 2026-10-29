# Avalonia 迁移功能回归深度 Review（2026-10-26）

**范围**：`master` → `feature/avalonia`（merge_base `794e956`），165 文件，+14097/-2042 行
**基准**：master（WinForms）为功能基准，逐行对比双实现语义
**结论**：约 **96 条 findings（5 critical / 16 high / ~35 medium / ~40 low）+ 44 条行为差异**。Avalonia 版当前核心交互链（右键菜单、样式快捷键、热键、保存）大面积缺失或失效。

---

## 🔴 Critical（5 项）

### C1. 空格预览死锁 ——「预览卡死」根因
- **位置**：`SETUNA.Platform.Windows/WindowsHotkeyService.cs:332` + `SETUNA.Avalonia/AvaloniaHost.cs:190` + `SETUNA.Avalonia/AvaloniaClotheslineController.cs:219`
- **根因**：`KeyHookProc` 只把 keydown 喂给过滤器（keyup 恒放行且不通知），`AvaloniaHost` 硬编码 `isDown: true` → 状态机 `OnKeyUp()` 永不执行，`_downSeen` 永不复位。第一次空格后，之后每次空格 down 都被当作 repeat 吞掉。
- **叠加**：Avalonia 版没有移植旧版 200ms 自动关闭计时器（`IsCursorOverPreview` 是死代码）→ 预览只能开不能关。
- **旧版**：`SETUNA/Main/Clothesline/ClotheslineSpaceKeyHook.cs:112-135` 完整处理 WM_KEYUP/SYSKEYUP 并喂状态机复位轮次。
- **修法**：把 keyup 事件接进 key filter 链（filter 增加 isDown 参数真实传递），并补自动关闭计时器。

### C2. WinForms 宿主全局热键全部静默失效
- **位置**：`SETUNA.Core/Main/Option/SetunaOption.cs:730`（`PlatformServices.IsRegistered` 门控）+ `SETUNA/Program.cs`（从不调用 `PlatformServices.Register()`，全仓只有 Avalonia `Program.cs:13` 注册）
- **后果**：WinForms 版注册被跳过却返回 true，Ctrl+D1/D2 等热键无任何反应无提示；`Mainform.WndProc` 的 `0x0312` 分支成死代码；`Mainform_Resize` 新加的"重注册热键"是无操作补丁。
- **修法**：WinForms 入口注册 PlatformServices（热键走 callback 链路），或恢复直调 RegisterHotKey 路径。

### C3. 单实例机制双宿主全断
- **位置**：`SETUNA.Core/com/clearunit/SingletonApplication.cs:62`（IPC 删除改本进程静态委托）+ `SETUNA.Avalonia/Program.cs`（完全无单实例逻辑）
- **后果**：可双开进程（双托盘图标、热键冲突）；Core 版第二实例 `Event` 恒为 null 静默退出；旧版"二次启动激活已运行实例 + 命令行转发"失效。
- **修法**：Avalonia 补单实例（Mutex + IPC 转发或管道），Core 恢复跨进程通知。

### C4. 保存流程 UI 线程死锁【待运行时验证】
- **位置**：`SETUNA.Core/Main/StyleItems/CImageStyleItem.cs:58` —— `GetSavePathAsync(scrap).GetAwaiter().GetResult()`
- **根因**：Avalonia `ShowSaveFileDialog` 是真异步（`AvaloniaHost.cs:365` await StorageProvider），任务等待用户选择，SaveAsync 在 UI 线程同步阻塞 → 经典死锁。覆盖确认弹窗路径同理。
- **后果**：Avalonia 下"保存"样式触发后界面永久卡死（WinForms 宿主 `Task.FromResult` 同步完成不受影响）。
- **修法**：SaveAsync 全异步化，CStyle.Apply 增加 async Task 路径。

### C5. 配置全损炸弹：Picasa 类型移出 extraTypes（high→按影响升格关注）
- **位置**：`SETUNA.Core/Main/StyleItems/StyleItemDictionary.cs:17`
- **后果**：配置过含 Picasa 样式的用户，SetunaConfig.xml 反序列化整体抛异常 → `LoadOption` catch → 全部配置回退默认，样式/热键/选项丢失。注释的"兼容旧配置"声明与实际行为相反。
- **修法**：把 `CPicasaUploaderStyleItem` 加回 `GetAllStyleItems()`（或单独加入 extraTypes 列表）。

---

## 🟠 High（16 项）

| # | 位置 | 问题 |
|---|------|------|
| H1 | `WindowsScreenCaptureService.cs:25` | `GetHdc()` 未配对 `ReleaseHdc()`（同文件 DrawCursorToImage 有配对，master 有配对）→ 截图高频使用 GDI 句柄耗尽 |
| H2 | `ScrapWindow.cs:452` | `Image` setter 不释放旧图像 → 每次裁剪/旋转泄漏全尺寸位图（+PNG 中间副本双倍） |
| H3 | `ShellNotifyIcon.cs:53` | 托盘窗口 message-only 收不到 `TaskbarCreated` → **Explorer 重启后托盘图标永久消失** |
| H4 | `WindowsClipboardService.cs:123` | `GetImage` 按 CF_DIB 短路返回 null → CF_BITMAP/PNG-only 剪贴板粘贴静默失败 |
| H5 | `AvaloniaHost.cs:610` | 热键注册失败反馈断裂：`ShowBalloon` 空实现，旧版弹 HotkeyMsg+回写禁用 |
| H6 | `AvaloniaHost.cs:628` | Function1 首按行为反转（旧版初始 true 首按隐藏全部；新版初始 false 首按无效果） |
| H7 | `AvaloniaHost.cs:488` | `AddImageList` 只接受 ScrapSourceImage → **拖文件/拖 URL 建贴图静默丢弃** |
| H8 | `ScrapWindow.cs:330` | `TargetLocation` 只记录不移动 → **移动参考图样式（CMoveStyleItem）完全失效** |
| H9 | `ScrapWindow.cs:579` | 取下挂绳时收缩窗恢复条件写反（检查已被隐藏的 IsVisible）→ 收缩状态丢失 |
| H10 | `ScrapWindow.cs:173` | Ctrl+双击挂绳不检查 ClotheslineEnabled → 模式关闭时贴图永久隐藏 |
| H11 | `TrimWindow.cs:42` | 裁剪窗口/选区坐标无 DPI 换算 → 150% 下裁剪区域错位 |
| H12 | `ClotheslineWindow.cs:166` | 预览锚点返回客户坐标而非屏幕坐标 → NearThumbnail 预览贴屏幕左上角 |
| H13 | `ci.yml:4` / `version-update.yml:5` | 触发分支 `main, ConvertNetCore, develop`，仓库实际 master/feature/* → **CI 从未运行** |
| H14 | `version-update.yml:70` | GITHUB_TOKEN 推 tag 不触发 release.yml（防递归）→ 发版链路断裂 |
| H15 | `release.yml:55` | `PublishTrimmed=true + TrimMode=link` 用于 WinForms（官方不支持）+ 反射序列化 → 发布产物必崩风险 |
| H16 | 所有发布流程 | 只构建 WinForms 版，Avalonia 项目无任何 CI/发布覆盖 |

---

## 🟡 Medium（35 项摘要）

**预览/晾绳**：捕获暂停/关闭模式不关预览（预览会被拍进截图）；绳子方向/预览位置设置改后不生效需重启；删除贴图不关预览；UpdateScrap 不刷新预览；SetItems/SetOrientation 不夹紧滚动偏移（摘图后显示空白）；ScrollToIndex 混用 DIP/物理像素；滚轮横向方向相反+竖向步进减半；拖绳每像素全量写盘；多显示器工作区恒取 [0]；锚点回退 (0,0) 而非鼠标位置。

**贴图窗口**：样式 `wait` 间隔契约被同步 foreach 丢弃（定时器样式瞬间连发、IsInitApply 过滤丢失）；RemoveStyle 空实现（还原过的图重启后又收缩）；关闭贴图不关收缩窗（孤儿窗口）；Manager setter 丢失按键转发；Refresh/RebuildViewBitmap 每次全图 PNG 编码卡顿；负 Scale 不缩小；托盘点击语义改变；贴图固定出现 (100,100) 而非光标处；截图恒抓主屏；"关闭所有截图"无确认框；启动截图无防重入；MessageBoxWindow 以隐藏窗为 owner 弹 ShowDialog（自家注释记录过的教训）；Function1 不记忆隐藏前状态。

**平台服务**：GCHandle 双重 Free【待验证】；TrackCommand 静态注册表只增不清；Task.Run 跨线程触碰 Avalonia Window【当前不可达】；SetImage 只投 CF_DIB 单格式；OpenClipboard 无重试。

**Core**：复制剪贴板每次 Clone 泄漏；CDustScrapStyle 丢 Suspend 包裹 + FromCurrentSynchronizationContext 风险；晾绳 Core/宿主双路径【待验证】；DownloadImage async void（当前零调用）；PSD 支持移除；保存 fire-and-forget 异常逃逸。

**WinForms 宿主**：启动无条件强制最小化进托盘（覆盖模式设置）；最小化被改隐藏+双气泡+句柄重建副作用；LayerManager CacheItem 空保护不完整（NRE 后移到 231 行）。

**测试**：OptimizeLayerCounter 算法测试被删换空壳；空格状态机测试与真实链路脱节（"全绿"掩盖 up 路径零调用）。

---

## 🟢 Low（40 项摘要）

Esc 关闭贴图失效；拖动无半透明反馈；收缩窗不反映缩放/边距样式且忽略双击点；预览窗多 1px 灰边框；发布版路径硬编码开发机 Debug 目录；ScrapAdded→RefreshTrayMenu 双重订阅+每次重读 ico；MonitorInfo.Scaling 恒 1.0；GetMessage bool 签名隐患；InstanceRef 永不清理/重复赋值；Dispose 不释放预览窗；ResolveVertical 潜在 NRE；CacheItem 非原子写+损坏 JSON 抛异常；迁移每项一次磁盘写；PaintWindow/Splash 无 Dispose；InterpolationMode 弱化为 object；ShowAllScrap 挂起跳过（有意变更需确认）；版本号三方不同步；release-helper.ps1 提示 push origin main 与实际 master 不符；prerelease publish --no-build 组合可能失败【待验证】；信息版本节点 sed 遗漏 AssemblyInformationalVersion。

---

## 行为差异总表（44 条，按域）

### 晾衣绳（14 条）
keyup 轮次配对、捕获暂停关预览、预览自动关闭、空格切换语义（Avalonia"命中另一张=切换" vs WinForms"一律收起"，与 commit 0438876 的说法冲突需裁决）、删除贴图联动、图像更新联动、滚动夹紧、滚轮方向/步进、位置持久化时机、设置实时应用、ScrollToScrap 缺失、预览定位基准、OriginalLocation 取值【待验证】、钩子失败降级。

### 贴图窗口（8 条）
空格预览开关（同上）、移动样式、裁剪交互（无灰化背景/键盘微调/尺寸提示）、边框边距样式无呈现、预览自动关闭（同上）、双击收缩定位、退出贴图去向不分 CloseReason、Esc/拖动反馈/负缩放三缺失。

### 宿主/平台（12 条）
**单实例+命令行转发缺失、右键样式菜单=NullMenuListener（右键无菜单）、样式快捷键（KeyItemBook）未接线、ClickCapture 未迁移、置顶自动维持（WindowManager）未迁移、回收站开关无效、版本信息菜单项缺失、文本路径粘贴缺失、Splash 未迁移、命令行参数无消费方、选项窗口状态处理缺失、ApplicationMode 模式切换缺失**。

### Core（6 条）
WinForms 热键、单实例二次启动、JPEG 保存静默失效、保存对话框 filter/DefaultExt 丢失（无扩展名命名模式产出无扩展名文件）、PSD、样式快捷键（同上）。

### WinForms 宿主（4 条）
热键失效、启动形态覆盖、最小化改隐藏、算法测试删除。

---

## 修复优先级

**P0（小 diff，先修）**
1. C1 空格 keyup：filter 链传真实 isDown，keyup 通知状态机；补 200ms 自动关闭
2. C2 WinForms 热键：WinForms 入口装配 PlatformServices
3. C3 单实例：Avalonia 补 Mutex/转发；Core 恢复跨进程通知
4. C4 保存死锁：SaveAsync 全异步化
5. C5 Picasa 类型加回 extraTypes（一行修复，防配置全损）
6. Avalonia 交互链补齐：右键菜单 listener、样式快捷键 keyBook、PreviewDialogFactory 注册

**P1（资源/稳定性）**
H1 HDC 释放、H2 Image setter 释放、H3 TaskbarCreated、H4 剪贴板格式、H15 移除 PublishTrimmed、H13-14 CI 触发分支/tag 令牌、H16 Avalonia CI、H8 移动样式、H11 裁剪 DPI

**P2**
其余 medium（预览联动、滚动、DPI、线程安全）批量清理；low 批量清理；ImageSharp 3.1.12 → 最新（5 个已知漏洞）

## 需产品裁决
- 空格预览切换语义：Avalonia"预览开着命中另一张=切换源" vs WinForms"一律收起"（0438876 声称对齐旧版但与仓库内基准相反）
- PSD 支持移除是否接受
- ShowAllScrap 跳过挂起贴图（有意变更）确认
