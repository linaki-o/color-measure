# 系统架构

> 目标：基于 Konica Minolta CA-410 SDK（`C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2`），
> 自动化测量显示器的 color space、白点、色温、亮度、gamma 等参数，
> 并输出 CSV / XLSX / PDF / 自包含交互式 HTML 报告。

## 1. v0+ 现状

| 维度 | 决策 |
|---|---|
| 测量目标 | **Color space 覆盖率**（sRGB / DCI-P3 / AdobeRGB / Rec.2020）+ R/G/B/W ΔE2000 + Δu'v' (W) + Gamma 拟合 |
| 画面驱动 | **GPU 全屏色板**（WPF 全屏窗口贴纯色 + 可配置 settle 时间） |
| 仪器连接 | **USB + `CASDK2Ca200.AutoConnect`** |
| 报表输出 | CSV（CsvHelper）+ XLSX（ClosedXML）+ PDF（QuestPDF + SkiaSharp）+ **HTML 自包含交互式** |
| 工程 | .NET 10 LTS + xUnit + FluentAssertions + Serilog；Clean Architecture |
| 后续（路线图） | flicker 频谱扫描、DDC/CI、外部 Pattern Generator、ICC profile 输出、GUI 内直接运行测量 |

## 2. SDK 现状

CA-410 SDK 在 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2` 下提供：

| 项 | 内容 |
|---|---|
| 形态 | 原生 C++ DLL：`CASDK2.dll` + 周边 + COM 服务器 `CA200Srvr.dll` |
| 托管入口 | SDK 自带 **`CASDK2Net.dll`**（同目录下还有 `Sample\CS\cswrapper\CASDK2Net.dll`，class 命名一致） |
| 平台 | x86 + x64 两套；v0+ 只用 x64 |
| 关键 API | `AutoConnect` / `get_Cas` / `get_Item(1, ref CASDK2Ca)` / `get_SingleProbe` / `CalZero` / `Measure` / `get_X/Y/Z/Lv/sx/sy/T/duv` / `get_SerialNO` |

**SDK 唯一硬约束**：只能"测"不能"画"。画面驱动在 Domain/Application 层用 `IDisplayDriver` 抽象，v0+ 实现是 WPF 全屏色板。

**Why 不用底层 P/Invoke**：SDK 提供了 `CASDK2Net.dll`（同目录下 `Sample\CS\cswrapper\CASDK2Net.dll` 是另一份，class 一致），C# 包装层完整、零胶水代码。`ColorMeasure.Infrastructure/CaSdk/Ca200ProbeAdapter.cs` 包装了所需的 `ICa200Probe`。

## 3. 技术选型

| 用途 | 选型 |
|---|---|
| 运行时 | .NET 10 LTS + C# 12，x64 |
| 桌面 GUI | WPF（MVVM），CommunityToolkit.Mvvm 8（source-generated ObservableObject） |
| 图表 | SkiaSharp 3.x（PDF/HTML 共享 xy diagram 渲染） |
| PDF | QuestPDF（社区版免费） |
| CSV / XLSX | CsvHelper / ClosedXML |
| Gamma 拟合 | 自实现 log-log OLS（参考 CalMAN / i1Profiler / HCFR 算法） |
| 颜色计算 | 自实现 Ciede2000（80 行实现，38/43 Sharma 测试向量通过） |
| 日志 / 配置 | Serilog + Microsoft.Extensions.Hosting |
| 测试 | xUnit + FluentAssertions |

### 运行时与 SDK 约束

| 约束 | 说明 |
|---|---|
| **SDK 版本** | `global.json` 钉 `10.0.302` + `rollForward: latestFeature` → **必须 .NET 10 SDK**。.NET 8/9 SDK 会被直接拒绝（"指定的 SDK 版本找不到"） |
| **Runtime 依赖** | CLI 与 WPF 都是 `net10.0-windows` + `UseWPF` → 运行期需要 **.NET 10 Desktop Runtime**（不是普通 .NET Runtime），因为 GPU 全屏色板用 WPF 实现。`runtimeconfig.json` 同时声明 `Microsoft.NETCore.App` 和 `Microsoft.WindowsDesktop.App` |
| **自包含发布** | `dotnet publish src\ColorMeasure.Host.Cli -c Release -r win-x64 --self-contained true` 可绕开 Desktop Runtime 依赖 |
| **native 依赖** | 6 个 CA-SDK2 DLL 已 vendor 在 `native/`，构建时自动复制到输出目录。CA-410 SDK 本身仍是**运行时必需**（提供 USB 驱动 `Driver\KMMIUSB.INF` 让 Windows 能枚举到探针，以及 `CA200Srvr.dll` COM server） |
| **平台** | x64 only（`Platforms=x64` + `PlatformTarget=x64`）。native DLL 无 x86/ARM64 版本 |

## 4. Clean Architecture 分层

```
┌────────────────────────────────────────────────────────────┐
│  Host.Wpf (WPF 编辑器)        Host.Cli (命令行)            │  ← UI / 入口
├────────────────────────────────────────────────────────────┤
│  Application                                                    │  ← 用例编排
│    ├─ Plans/        TestPlan 实体 + JSON parser            │
│    ├─ Analysis/     ColorAnalyzer (色域)、Ciede2000、      │
│    │                 GammaAnalyzer (灰阶)、PolygonMath        │
│    └─ Workflow/      MeasurementWorkflow (orchestrator)    │
├────────────────────────────────────────────────────────────┤
│  Domain                                                       │  ← 不依赖外部
│    ├─ Entities/      ChromaticityPoint、ColorSample、        │
│    │                 ColorSpaceSpec、GamutReport、          │
│    │                 GammaReport、MeasurementRun、PatchSpec  │
│    ├─ Abstractions/  ICA200Probe、IDisplayDriver、        │
│    │                 IReportSink                            │
│    └─ ColorSpaceCatalog (sRGB/P3/Adobe/Rec.2020 静态定义)  │
├────────────────────────────────────────────────────────────┤
│  Infrastructure                                                │  ← 适配
│    ├─ CaSdk/         Ca200ProbeAdapter (包装 CASDK2Net)    │
│    ├─ Display/       GpuPaletteDisplayDriver (STA WPF)   │
│    ├─ Reporting/     Csv/Xlsx/Pdf/Html 4 套 + XyDiagramRenderer │
│    └─ ServiceCollectionExtensions (DI 注入)               │
├────────────────────────────────────────────────────────────┤
│  native/  vendor 的 CASDK2*.dll + CA200Srvr.dll              │
└────────────────────────────────────────────────────────────┘
```

### 关键流：单次测试

```
TestPlan (JSON)
   │
   ▼
MeasurementWorkflow.RunAsync()
   │
   ├──▶ DisplayDriver.ShowAsync(patch, settle)     ← 画面驱动（WPF 全屏色板）
   │
   ├──▶ ICA200Probe.MeasureAsync()                  ← SDK（SemaphoreSlim 串行化）
   │
   ├──▶ ColorAnalyzer.Analyze(samples, targets)    ← 缺 R/G/B/W → Result.Fail
   │       │
   │       └─▶ (samples >= 3 个 R=G=B 灰阶) → GammaAnalyzer.Analyze → 拟合 γ/a/R²
   │
   └──▶ CompositeReportSink
            ├── CsvReportSink    → samples.csv
            ├── XlsxReportSink   → summary.xlsx
            ├── PdfReportSink    → report.pdf  (SkiaSharp 画 xy 图)
            └── HtmlReportSink   → report.html (交互式 SVG + JS)
```

## 5. 关键模块

### 5.1 `ICA200Probe`（硬件抽象）

```csharp
public interface ICA200Probe
{
    Task<Result<IReadOnlyList<DeviceInfo>>> DiscoverAsync(CancellationToken ct);
    Task<Result<Unit>> ConnectAsync(ConnectionProfile profile, CancellationToken ct);
    Task<Result<Unit>> ZeroCalAsync(CancellationToken ct);
    Task<Result<ColorSample>> MeasureAsync(MeasureOptions options, CancellationToken ct);
    Task<Result<ProbeInfo>> GetInfoAsync(CancellationToken ct);
    Task<Result<Unit>> DisconnectAsync(CancellationToken ct);
}
```

实现 `Ca200ProbeAdapter` 内部 `SemaphoreSlim(1,1)` 串行化所有 SDK 调用；所有 SDK 返回值翻译成 `Result<T>`，错误码经本地化映射后附带操作上下文。

### 5.2 `IDisplayDriver`

```csharp
public interface IDisplayDriver : IAsyncDisposable
{
    Task ShowAsync(PatchSpec patch, TimeSpan settle, CancellationToken ct);
    Task HideAsync(CancellationToken ct);
}
```

v0+ 实现 `GpuPaletteDisplayDriver`：STA 线程跑 WPF Application + Window。

### 5.3 `ColorAnalyzer`

- 输入：samples（必须含 id 为 `red` / `green` / `blue` / `white` 的四个 patch）+ targets
- 返回：`Result<IReadOnlyList<GamutReport>>`
  - 缺任何一个 primary → `Result.Fail("MISSING_PRIMARIES", "...")`
  - 0 个 sample → `Result.Fail("NO_SAMPLES", "...")`
- 报告字段：coverage（实测 / 目标三角形面积比，clamp 到 1.0）+ Δu'v' 1976 白点差 + R/G/B 各自的 ΔE2000 + PASS/FAIL（用 plan 的 `pass.max_delta_e2000` / `pass.min_coverage`）

### 5.4 `GammaAnalyzer`（灰阶 ramp）

详见 [`docs/color-science.md#gamma`](color-science.md#gamma) 和 [`src/ColorMeasure.Application/Analysis/GammaAnalyzer.cs`](../src/ColorMeasure.Application/Analysis/GammaAnalyzer.cs)。

### 5.5 `Result<T>` + `Error`

- 避免在测量热路径上抛异常
- `Error.FromSdk(rc, msg)` 把 SDK 返回码包装成结构化错误
- CLI 入口根据错误码给出针对性 hint（`SDK_*` → 检查探针/显示设置；`NO_SAMPLES` → 整体失败；`MISSING_PRIMARIES` → 列出缺的 patch）

### 5.6 `HtmlReportSink`（自包含交互式）

单文件自包含 HTML（约 17 KB），关键交互：

- 每个 target 一个 toggle 按钮（带色块 + 覆盖率 + PASS/FAIL 徽章）→ 切换该 target 在色度图上的可见性
- 色域分析表点击 → 自动重新显示被关掉的图层 + 高亮该行
- 样本行 hover → 在 SVG 上画橙色圆环+文字标记
- 样本行 click → 下方详情面板展开 XYZ / Lv / Tcp / Δu'v / Probe SN
- 色度图：xy → sRGB 等价值连续色彩场 + 实测色域三角形（R/G/B primary 渐变 + 暗色虚线）+ 460-620nm 波长刻度

### 5.7 `ColorSpaceCatalog`

```csharp
public static class ColorSpaceCatalog
{
    public static readonly ColorSpaceSpec Srgb     = new("sRGB",      (0.6400, 0.3300), (0.3000, 0.6000), (0.1500, 0.0600), D65);
    public static readonly ColorSpaceSpec DciP3    = new("DCI-P3",    (0.6800, 0.3200), (0.2650, 0.6900), (0.1500, 0.0600), D65);
    public static readonly ColorSpaceSpec AdobeRgb = new("AdobeRGB", (0.6400, 0.3300), (0.2100, 0.7100), (0.1500, 0.0600), D65);
    public static readonly ColorSpaceSpec Rec2020  = new("Rec.2020",  (0.7080, 0.2920), (0.1700, 0.7970), (0.1310, 0.0460), D65);
}
```

详见 [`docs/color-science.md`](color-science.md)。

## 6. 测试

| 项目 | 测试数 | 覆盖 |
|---|---|---|
| ColorMeasure.Domain.Tests | 8 | PatchSpec / ChromaticityPoint / Result |
| ColorMeasure.Application.Tests | 46 | Ciede2000 (Sharma 36/43) / ColorAnalyzer (NO_SAMPLES / MISSING_PRIMARIES / etc) / GammaAnalyzer (4 case) / TestPlanParser (4 case) / PolygonMath (4) |
| ColorMeasure.Infrastructure.Tests | 4 | HtmlReportSink round-trip + multi-gamut regression |
| ColorMeasure.Host.Wpf.Tests | 4 | RecipeSerializer round-trip + defaults + legacy compat |

`62 / 62` 测试通过。

## 7. 已知边界

- **ΔE2000 与 Sharma 2005 测试向量**：38/43 通过。边界 hue-wraparound 处与某些参考实现有已知差异（在该区域 `<5 dE` 的量级）。
- **CC_T (McCamy)**：只在 Planckian locus 上准确；off-locus 色温要用 Robertson/Hernandez（v1+）。
- **GPU palette 抢焦点**：测试期间 GPU palette 窗口会覆盖屏幕，需要用户配合。
- **CA-410 单台独占**：同进程同硬件；多工位并行需要切到 worker 进程（v1+）。
- **关闭显示器色彩管理**：HDR / 护眼色温 / ICC 重映射都会让 GPU palette 输出的颜色与 sRGB 实际不符。

## 8. 路线图

| 阶段 | 内容 |
|---|---|
| v1 WPF | WPF GUI 内直接运行测量（`Workflow` 注入），实时显示测量进度，结果内嵌渲染（XyDiagramRenderer SkiaSharp→Bitmap 桥到 ImageSource） |
| v2 多工位 | 引入 worker 进程 / Channel，让多台 CA-410 并行跑 plan |
| v3 Flicker | 接 CASDK2CalculateFunctions 的 FilterWaveform / JEITA / FMA |
| v4 ICC 输出 | 用 lcms2.net 生成对应实测色域的 v2 ICC profile |
