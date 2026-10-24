# SETUNA 跨平台迁移：期 1-3（WinForms → Avalonia）

> 计划文档存放于本文件（用户全局规则要求 plan 文档放 `doc/plans`，plan 模式只允许写本文件 — 批准后第一步会把本计划复制到 `E:\Git\SETUNA2\doc\plans\avalonia-migration-phase1-3.md`）。

## Context

用户希望 SETUNA（Windows 贴图工具，WinForms/WPF，net9.0-windows，214 个 .cs ≈ 32k 行）支持全平台。经讨论确定方案：**Avalonia UI + 平台服务层**。本次在新分支上实施期 1-3，Windows 先行，为 macOS/Linux 留接口。**功能必须与现有版本一致**，旧 WinForms 版全程保持可编译作为验收基准。

已确认决策：期 1-3 先行；Picasa 迁入 Core 但不注册（服务已死）；mac/Linux 只定义接口不写实现。

## 现状关键事实（调查结论）

- 120/214 个文件引用 WinForms；81 个引用 System.Drawing；GDI+ 是全局图像模型（ImageSharp 仅用于 TGA 解码）
- 业务与 UI 焊死：`ScrapBase`(1192 行) 既是窗体又是状态机；`CStyleItem.Apply(ref ScrapBase)` 直接持有窗体 → 必须引入 `IScrap` 接口隔离
- 平台硬依赖集中可枚举：`WindowsAPI.cs`（15 个 user32 P/Invoke）、`CaptureForm.cs`（BitBlt 截屏+窗口枚举）、`SetunaOption.cs:745`（RegisterHotKey）、`Mainform.cs:649`（WndProc 0x312）、`AutoStartup.cs`（注册表）、`ClotheslineSpaceKeyHook.cs`（SetWindowsHookEx LL 钩子）、`ClickCapture.cs`/`ScrapDrawForm.cs`（SetWindowPos/GetDC）、`WebPWrapper.cs`（libwebp dll）
- 配置：XmlSerializer → `SetunaConfig.xml`（exe 旁）；缓存：`%LocalAppData%\SETUNA`（PNG+JSON）；单实例：命名 Mutex（`SingletonApplication.cs` 已是简化版）
- 托盘：NotifyIcon（Mainform.Designer.cs:25）；置顶轮询：windowTimer 500ms（Mainform.Designer.cs:70）；透明：`TransparencyKey=Fuchsia`（晾绳）+ Opacity 三态动画（ScrapBase timOpacity 15ms）
- 测试基线：80 个测试全绿；构建 0 错误
- Avalonia 11 事实：TrayIcon/Screens/TransparencyLevelHint 内置；**透明点击穿透无跨平台 API**（文档明示）；屏幕捕获无内置 API → 均走平台服务层

## 目标架构

```
SETUNA2/
├─ SETUNA/                      # 旧 WinForms 版（冻结，全程可编译 = 验收基准）
├─ SETUNA.Core/                 # 业务库（net9.0-windows classlib；含 Platform/ 接口）
├─ SETUNA.Platform.Windows/     # 六个平台服务接口的 Windows 实现（P/Invoke 集中地）
├─ SETUNA.Avalonia/             # Avalonia UI 壳（WinExe，入口项目）
└─ SETUNATests/                 # 改引 Core，80 测试保持全绿
```

### 平台服务接口（`SETUNA.Core/Platform/`，6 个 + 聚合门面）

| 接口 | 方法 | 替代的现有代码 |
|---|---|---|
| `IHotkeyService` | `Register(HotkeySpec, Action)` / `Unregister(HotkeyId)` / `SetKeyFilter(Func<int,bool>)`（LL 钩子） | SetunaOption.cs:709-750、Mainform WndProc、ClotheslineSpaceKeyHook |
| `IScreenCaptureService` | `CaptureScreen(Rectangle): Bitmap` / `GetMonitors()` | CaptureForm.cs:314 BitBlt、Screen.AllScreens |
| `IWindowService` | `SetTopmost/SetClickThrough/SetLayeredOpacity/SetWindowPos/GetForegroundWindow/EnumWindows(filter)` | WindowsAPI.cs、ScrapBase SetWindowLong、ClickCapture SetWindowPos |
| `ITrayService` | `Initialize(spec)/ShowBalloon/Dispose` | NotifyIcon |
| `IAutoStartService` | `SetEnabled(bool)/IsEnabled` | AutoStartup.cs |
| `IClipboardService` | `SetImage/GetImage/SetText/GetText/ContainsImage`（GDI Bitmap 进出） | WinForms Clipboard 各调用点 |

- 聚合：`IPlatformService { IHotkeyService Hotkey; ... }`，`static IPlatformService Instance`，Program.cs 按 OS 装配。不引 DI 容器（与现有 static 单例风格一致）。
- **Core 保持 `net9.0-windows` + `UseWindowsForms=true`**（Keys/Bitmap 仍在业务层），这是刻意分两层走：本迁移不动图像模型，跨平台期再换 ImageSharp。

## 实施步骤

### 期 1：骨架与平台服务（旧版零改动）

1. 新建分支 `feature/avalonia`（基于 ConvertNetCore @ ec47a86）
2. 建三个项目（命名/TFM 按上文），加入 sln
3. `SETUNA.Core/Platform/` 写 6 接口 + 数据类型（HotkeySpec/MonitorInfo/WindowInfo/TrayIconSpec）
4. `SETUNA.Platform.Windows` 实现全部 6 接口：P/Invoke 从 `WindowsAPI.cs`/`CaptureForm.cs`/`SetunaOption.cs`/`AutoStartup.cs`/`ClotheslineSpaceKeyHook.cs` **复制**（旧文件不动）；热键用 message-only window（CreateWindowEx HWND_MESSAGE）收 WM_HOTKEY，不绑 Avalonia 窗口
5. 验收：`dotnet build` 全 solution 绿；旧版无变化

### 期 2：业务代码迁移到 Core（保持命名空间不变）

6. git mv 约 90 个业务文件到 `SETUNA.Core/`（Option/Cache/Style/StyleItems 命令部分/KeyItems/Clothesline 纯逻辑/ScrapSource*/com.clearunit/Plugins/监听接口），旧 csproj 加 ProjectReference，零代码修改照常编译
7. **`IScrap` 接口**：提取 `ScrapBase` 被 Style 体系使用的成员（事件 12 个 + 属性/方法约 30 个，源：`CStyleItem.Apply`、`CStyle`、`ScrapBook` 调用面）；`Apply(ref ScrapBase)` 签名改 `Apply(IScrap)`；`ScrapBase : BaseForm, IScrap`（旧版自动满足）
8. `SetunaOption.cs` 中的 RegisterHotKey P/Invoke 改调 `IPlatformService.Instance.Hotkey`（保留公开方法签名）
9. `LayerManager`/`WindowManager` 的 `Form`/`IntPtr` 操作改走 `IWindowService`；`AutoStartup` 逻辑搬入平台实现
10. Picasa 5 文件迁入 Core，`StyleItemDictionary.GetAllStyleItems()` 注释掉 CPicasaUploaderStyleItem 注册
11. SETUNATests 引用改指 Core，跑绿
12. 验收：旧 WinForms 版启动/截图/贴图/热键全流程手测无回归

### 期 3：Avalonia 主流程（第一个可运行里程碑）

13. `SETUNA.Avalonia`：Program.cs（AppBuilder + 装配 WindowsPlatformService）+ App.axaml + 单实例接入 + GDI↔Avalonia Bitmap 转换器（`Bridging/GdiBitmapConverter.cs`，PNG 流编解码实现，避免逐像素拷贝）
14. `ScrapWindow`（对应 ScrapBase）：`SystemDecorations=None` + `TransparencyLevelHint=Transparent` + `Topmost` + Opacity 三态动画（照搬 timOpacity 逻辑）+ 拖动/双击/右键 ContextMenu（绑 CStyle 菜单树）+ 位置持久化（**DIP↔物理像素换算**，读写配置时乘 `Screen.Scaling`，保证旧配置兼容）
15. `MainWindow`：隐藏窗体宿主 + TrayIcon（Avalonia 内置）+ 热键触发 StartCapture 骨架 + windowTimer 500ms 置顶轮询
16. 贴图生命周期：从剪贴板/文件创建贴图、拖动、右键关闭、回收站队列、重启恢复（读缓存）走通
17. 验收（对照旧版逐项）：
    - 启动 → 托盘出现 → 热键截图（简单矩形）→ 贴图出现 → 拖动 → 右键关闭
    - 剪贴板粘贴创建贴图；拖文件创建贴图
    - 重启后贴图从缓存恢复，位置一致
    - 旧 SetunaConfig.xml 能被读取
    - `dotnet test` 80+ 全绿；`dotnet build` 全绿

## 期 4-6（本次不做，架构已预留）

期 4 Style 全量（ToolBox/裁剪/绘图编辑器）；期 5 截图遮罩全屏流程+晾绳透明窗；期 6 OptionForm/自启 UI/打包脚本切换。mac/Linux 平台实现与图像栈换 ImageSharp 均在此后。

## 风险与对策

| 风险 | 对策 |
|---|---|
| ScrapBase 事件扇出（Mainform 订阅 12+ 事件）签名破坏 | IScrap 保留全部事件签名原样，期 2 编译验证 |
| DIP 坐标换算漏乘 Scaling 导致贴图错位/缩放漂移 | 期 3 验收含"旧配置位置一致"专项；换算集中在一个 helper |
| Z 序：LayerManager 逐窗体 SetWindowPos，Avalonia Topmost 布尔不够 | 句柄走 `TryGetPlatformHandle()`，Z 序经 IWindowService 直接 P/Invoke |
| 晾绳 TransparencyKey 无等价 | 期 5 才涉及；改 alpha=0 全透明渲染，接口已隔离 |
| GDI Bitmap 跨线程释放崩溃（反编译遗留） | 转换器统一 UI 线程 marshal，原有 try-catch 保留 |
| Costura.Fody 不适用于新项目 | 期 3 不打包；期 6 用 PublishSingleFile 替代 |

## 验证方式

1. 每期结束 `dotnet build SETUNA2.sln`（0 错误）+ `dotnet test`（80 测试全绿）
2. 期 2 手测旧版全流程（截图/贴图/热键/配置读写）
3. 期 3 按"期 3 验收清单"对照旧版手测 Avalonia 版
4. 提交节奏：每期至少一个 commit（含 `Co-Authored-By: Claude Code <noreply@anthropic.com>`）
