# Display Color Measure — 架构与技术选型

> 目标：基于 Konica Minolta CA-410 SDK（`C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2`），
> 自动化测量显示器的 color space、白点、色温、亮度、对比度、flicker、gamma 等参数，
> 并生成 CSV / XLSX / PDF / 自包含交互式 HTML 报告。

## 0. v0 Scope（已实现并验证）

| 维度 | 决策 |
|---|---|
| 测量目标 | **Color space 覆盖率**（sRGB / DCI-P3 / AdobeRGB / Rec.2020）+ 配套 R/G/B/W dE2000 + Δu'v'(W) |
| 画面驱动 | **GPU 全屏色板**（WPF 全屏窗口贴纯色 + 可配置 settle） |
| 仪器连接 | **USB + `CASDK2Ca200.AutoConnect`** |
| 报表输出 | CSV (CsvHelper) + XLSX (ClosedXML) + PDF (QuestPDF + SkiaSharp) + **HTML 自包含交互式** |
| 工程 | .NET 10 (LTS) + xUnit + FluentAssertions + Serilog；Clean Architecture 四层 |
| 后续阶段扩展项（v0 不做） | flicker 频谱扫描、DDC/CI、外部 Pattern Generator、ICC profile 输出、WPF GUI |

---

## 1. SDK 现状

CA-410 SDK 在 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2` 下提供：

| 项 | 内容 |
|---|---|
| 形态 | 原生 C++ DLL：`CASDK2.dll` + 周边 + COM 服务器 `CA200Srvr.dll` |
| 托管入口 | SDK 自带 **`CASDK2Net.dll`**（同目录下还有 `Sample\CS\cswrapper\CASDK2Net.dll`，class 命名一致） |
| 平台 | x86 + x64 两套；v0 只用 x64 |
| 关键 API（包装后） | `AutoConnect` / `get_Cas` / `get_Item(1, ref CASDK2Ca)` / `get_SingleProbe` / `CalZero` / `Measure` / `get_X/Y/Z/Lv/sx/sy/T/duv` / `get_SerialNO` |

**SDK 唯一硬约束**：只能"测"不能"画"。画面驱动在 Domain/Application 层用 `IDisplayDriver` 抽象，v0 实现是 WPF 全屏色板。

---

## 2. 技术选型

### 2.1 运行时 / 语言：**.NET 8/10 (LTS) + C# 12**，x64

- 官方托管入口 `CASDK2Net.dll` → 最少胶水
- 桌面 / CLI / CI runner 共用同一 Core 库
- SDK 调用阻塞 + 硬件独占 → 进程内串行化，不服务化

排除：C++/MFC（维护成本高）、Python（多一层 `clr`/`comtypes`）、Electron（与硬件无关，徒增体积）。

### 2.2 GUI：**WPF（MVVM）+ 可选 Avalonia**（v1 预留）

### 2.3 图形 / 报告

| 用途 | 库 |
|---|---|
| PDF CIE 1931 xy 图 + 色域 | **SkiaSharp 3.x**（独立配色实线/虚线、图例） |
| PDF 文档 | **QuestPDF**（社区版免费） |
| CSV | **CsvHelper** |
| XLSX | **ClosedXML** |
| 交互式 HTML | **原生 SVG + 原生 JS**，零外部依赖，单文件 ~17 KB |
| 色度计算 | 自实现（Ciede2000 / McCamy / Shoelace / u'v' 1976） |

### 2.4 工程基础

- **解决方案结构**：Clean Architecture：`Domain` / `Application` / `Infrastructure` / `Host.Wpf` / `Host.Cli` / `Tests.{Domain,Application,Infrastructure}`
- **DI / 配置 / 日志**：`Microsoft.Extensions.Hosting` + `Serilog`
- **测试**：`xUnit` + `FluentAssertions`
- **打包**：自包含单文件 `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`

### 2.5 SDK 接入

- vendor `CASDK2*.dll` + `CA200Srvr.dll` 到仓库 `native/`
- Infrastructure csproj `<Reference Include="CASDK2Net">` + `<None Include="native\*.dll"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`
- **CLI csproj 同时再链一次**这些 DLL——`dotnet run` 默认按 AnyCPU 找 `bin\Debug\...`，而 Infrastructure 强制 `x64` 输出 `bin\x64\Debug\...`；不重复链一遍会报 `FileNotFoundException`
- `ICA200Probe` 接口隔离 SDK 类型；错误码通过 `CASDK2_GetLocalizedErrorMsgFromErrorCode(2, …)` 映射为中文

---

## 3. 架构分层

```
┌───────────────────────────────────────────────────────────────┐
│  Host.Wpf (WPF + MVVM)        Host.Cli (命令行 / CI 入口)      │
├───────────────────────────────────────────────────────────────┤
│  Application                                                    │
│   • TestPlans (recipe JSON)                                     │
│   • MeasurementWorkflow   ──→  Channel<MeasureCommand>          │
│   • ColorAnalyzer (coverage / ΔE2000 / Δu'v' / CCT)             │
│   • Reporting hooks                                          │
├───────────────────────────────────────────────────────────────┤
│  Domain                                                          │
│   • Entities: ColorSample, ChromaticityPoint, ColorSpaceSpec,  │
│               GamutReport, MeasurementRun                       │
│   • Abstractions: ICA200Probe, IDisplayDriver, IReportSink     │
│   • Result<T> + Error                                          │
├───────────────────────────────────────────────────────────────┤
│  Infrastructure                                                  │
│   • CaSdk/Ca200ProbeAdapter       (包 CASDK2Net.dll)           │
│   • Display/GpuPalette/GpuPaletteDisplayDriver (WPF 全屏)      │
│   • Reporting/CsvReportSink, XlsxReportSink, PdfReportSink,    │
│                    HtmlReportSink                              │
│   • Reporting/IXyDiagramRenderer + XyDiagramRenderer (SkiaSharp)│
├───────────────────────────────────────────────────────────────┤
│  native/  vendor 的 CASDK2*.dll + CA200Srvr.dll                │
└───────────────────────────────────────────────────────────────┘
```

### 关键流：单次测试

```
TestPlan (JSON)
   │
   ▼
MeasurementWorkflow.RunAsync()
   │
   ├──▶ DisplayDriver.ShowAsync(patch, settle)     ← 画面驱动
   │
   ├──▶ ICA200Probe.MeasureAsync()                  ← SDK (SemaphoreSlim 串行化)
   │
   ├──▶ ColorAnalyzer.Analyze(samples, targets)    ← 缺 R/G/B/W → Result.Fail
   │
   └──▶ CompositeReportSink
            ├── CsvReportSink    → samples.csv
            ├── XlsxReportSink   → summary.xlsx
            ├── PdfReportSink    → report.pdf  (SkiaSharp 画 xy 图)
            └── HtmlReportSink   → report.html (交互式 SVG + JS)
```

---

## 4. 关键模块

### 4.1 `ICA200Probe`（硬件抽象）

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

### 4.2 `IDisplayDriver`

- `ShowAsync(PatchSpec, TimeSpan settle, CancellationToken)` → 切窗口背景 + 等 settle
- `HideAsync` → 还原黑屏
- v0 实现 `GpuPaletteDisplayDriver`：STA 线程跑 `Application` + `Window`

### 4.3 `ColorAnalyzer`

- 输入：samples（必须含 id 为 `red` / `green` / `blue` / `white` 的四个 patch）+ targets
- 返回：`Result<IReadOnlyList<GamutReport>>`
  - 缺任何一个 primary → `Result.Fail("MISSING_PRIMARIES", "...")`
  - 0 个 sample → `Result.Fail("NO_SAMPLES", "...")`
- 报告字段：coverage（实测 / 目标三角形面积比，clamp 到 1.0）+ Δu'v' 1976 白点差 + R/G/B 各自的 ΔE2000 + PASS/FAIL（用 plan 的 `pass.max_delta_e2000` / `pass.min_coverage`）

### 4.4 `Result<T>` + `Error`

- 避免在测量热路径上抛异常
- `Error.FromSdk(rc, msg)` 把 SDK 返回码包装成结构化错误
- CLI 入口根据错误码给出针对性 hint（`SDK_*` → 检查探针/显示设置；`NO_SAMPLES` → 整体失败；`MISSING_PRIMARIES` → 列出缺的 patch）

### 4.5 `HtmlReportSink`

单文件自包含 HTML（约 17 KB），关键交互：

- 每个 target 一个 toggle 按钮（带色块 + 覆盖率 + PASS/FAIL 徽章）→ 切换该 target 在色度图上的可见性
- 色域分析表点击 → 自动重新显示被关掉的图层 + 高亮该行
- 样本行 hover → 在 SVG 上画橙色圆环+文字标记 `(id, x.xxx, y.yyy)`
- 样本行 click → 下方详情面板展开 XYZ / Lv / Tcp / Δu'v / Probe SN

实现细节：

- 数据用 camelCase record 嵌入（避免 `x` 与 `X` 的命名冲突 → 用 `triX/triY/triZ`）
- SVG 用 `<polygon>` + `<circle>`，JS 通过 `setAttribute('display','none')` 切换
- 全部客户端，无 fetch/XHR

---

## 5. TestPlan 完整字段

```jsonc
{
  "name": "ColorSpaceCoverage-sRGB",  // 必填
  "connect": "auto",                  // "auto" | "usb"
  "calibrateBeforeStart": true,       // 开始前先做 ZeroCal
  "settleMs": 1500,                   // 贴色后等待时间
  "targets": ["sRGB", "DCI-P3", "AdobeRGB", "Rec.2020"],  // 可选，默认 ["sRGB"]
  "patches": [
    {                                  // 必填，id 必须包含 red/green/blue/white
      "id": "white",
      "hex": "#FFFFFF",                // #RGB / RRGGBB
      "avg": 8,                        // 平均次数（传给 SDK 的 averaging count）
      "label": "white"                 // 可选
    }
  ],
  "pass": {
    "max_delta_e2000": 5.0,            // 主点 ΔE2000 上限
    "min_coverage": 0.90               // 覆盖率下限
  }
}
```

未知 target 名 / 缺 primary / hex 非法 → 解析阶段就 `Result.Fail`，CLI 退出码 2（计划错误）。

---

## 6. 已知问题与边界

- **ΔE2000 与 Sharma 2005 测试向量**：38/43 通过（边界 hue-wraparound 处与某些参考实现有已知差异）
- **CC_T (McCamy)**：只在 Planckian locus 上准确；D65 等 off-locus 色温要用 Robertson/Hernandez（v1）
- **GPU palette 抢焦点**：测试期间 GPU palette 窗口会覆盖屏幕，需要用户配合
- **CA-410 单台独占**：同进程同硬件；多工位并行需要切到 worker 进程（v1）
- **关闭显示器色彩管理**：HDR / 护眼色温 / ICC 重映射都会让 GPU palette 输出的颜色与 sRGB 实际不符

## 7. 后续路线

| 阶段 | 内容 |
|---|---|
| v1 WPF GUI | 用 MVVM 重做 GUI，把 workflow + 进度条 + 实时色度图集成进窗口 |
| v2 多工位 | 引入 worker 进程 / Channel，让多台 CA-410 并行跑 plan |
| v3 Flicker | 接 CASDK2CalculateFunctions 的 FilterWaveform / JEITA / FMA |
| v4 ICC 输出 | 用 lcms2.net 生成对应实测色域的 v2 ICC profile |
