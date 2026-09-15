# 使用

## CLI

```
color-measure [options]

  --plan <plan>      测试计划 JSON（必填）
  --out <dir>         输出目录，默认 runs\<yyyyMMdd-HHmmss>
  --connect <mode>    auto|usb（默认 auto）
  --dry-run           只解析 plan，不连硬件
  --version
  -? | -h | --help
```

### 退出码

| 退出码 | 含义 |
|---|---|
| `0` | 成功（含 `--dry-run`） |
| `1` | 运行失败，错误码出现在日志中：`NO_SAMPLES` / `MISSING_PRIMARIES` / `SDK_*` / `INVALID_PLAN` / `GAMMA_TOO_FEW` 等 |
| `2` | 计划解析失败（recipe 文件不存在或 JSON 错） |
| `99` | 未捕获异常 |

### WPF 编辑器

```powershell
dotnet run --project src\ColorMeasure.Host.Wpf
# 或
src\ColorMeasure.Host.Wpf\bin\x64\Debug\net10.0-windows\ColorMeasure.Host.Wpf.exe
```

详见 [`docs/wpf-editor.md`](wpf-editor.md)。

## TestPlan 字段

完整示例见 `recipes/color-space-coverage-srgb.json`。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 计划名（出现在报告封面） |
| `connect` | string | 否 | `auto` 或 `usb`，默认 `auto` |
| `calibrateBeforeStart` | bool | 否 | true → 跑 plan 前先做 ZeroCal |
| `settleMs` | int | 否 | 每 patch 等待时间（ms），默认 1500 |
| `targets` | string[] | 否 | 目标色域数组，默认 `["sRGB"]`；支持 `sRGB` / `DCI-P3` / `AdobeRGB` / `Rec.2020` |
| `patches` | object[] | 是 | 至少包含 `id` 为 `red` / `green` / `blue` / `white` 的四个 patch；想做 gamma 加 ≥3 个 R=G=B 灰阶 |
| `pass` | object | 否 | 通过条件：`max_delta_e2000` + `min_coverage`，缺省则该项不参与判定 |

### Patch 字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | 是 | 唯一 id；`red` / `green` / `blue` / `white` 触发色域分析；`#RRGGBB` 或 `gray-NN` 触发 gamma 拟合 |
| `hex` | string | 是 | 颜色 `#RGB` 或 `#RRGGBB` |
| `avg` | int | 否 | SDK averaging count（每次测量内部平均多少次），默认 8 |
| `label` | string | 否 | 备注，出现在报告样本表 |

### Pass 字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `max_delta_e2000` | number | 否 | 主点 ΔE2000 上限，留空不参与判定 |
| `min_coverage` | number | 否 | Coverage 下限（0..1），留空不参与判定 |

### JSON Schema 与向后兼容

- `targets` 留空 → 默认 `["sRGB"]`
- `pass` 整段留空 → max=5.0 dE / min=0.90 coverage 都不会被强制检查（仅作为"显示参考"显示在报告中）
- 缺 `pass` key 的旧 recipe → 加载时同样回退到上述默认
- WPF 编辑器保存时：两个阈值都缺省时**不写** `pass` 块（与 CLI 默认行为一致）

## 完整示例

```json
{
  "name": "ColorSpaceCoverage-sRGB",
  "connect": "auto",
  "calibrateBeforeStart": true,
  "settleMs": 1500,
  "targets": ["sRGB", "DCI-P3", "AdobeRGB", "Rec.2020"],
  "patches": [
    { "id": "white",  "hex": "#FFFFFF", "avg": 8, "label": "white" },
    { "id": "red",    "hex": "#FF0000", "avg": 8, "label": "red primary" },
    { "id": "green",  "hex": "#00FF00", "avg": 8, "label": "green primary" },
    { "id": "blue",   "hex": "#0000FF", "avg": 8, "label": "blue primary" },
    { "id": "gray-25", "hex": "#404040", "avg": 4 },
    { "id": "gray-50", "hex": "#808080", "avg": 4 },
    { "id": "gray-75", "hex": "#C0C0C0", "avg": 4 }
  ],
  "pass": {
    "max_delta_e2000": 5.0,
    "min_coverage": 0.90
  }
}
```

## 典型 recipe

仓库 `recipes/` 目录自带：

| 文件 | 用途 |
|---|---|
| `color-space-coverage-srgb.json` | 默认：4 个原色 + 3 步灰阶。色域覆盖率 + ΔE2000 + 简单 gamma 估计 |
| `gamma-ramp.json` | 11 步灰阶 + sRGB primary。专门测 gamma 曲线（更准确的 R²） |

更多写法（单通道 ramp、混合模式）见 [`docs/recipes.md`](recipes.md)。

## 诊断 tip

```powershell
# 1) 验证 recipe 写得对（不接硬件）
color-measure --plan my.json --dry-run

# 2) 接 CA-410 但只测 1 个 patch 试通信
#    临时把 recipe 改成只留 white 一个 patch

# 3) 看 SDK 返回的错误码
color-measure --plan my.json --out runs\diag
# 日志里会有 rc=xxx, msg=...

# 4) HTML 报告里：
#    - 全部 4 个色域都画了但测量数据聚在 D65 附近 → GPU palette 窗口没覆盖探针
#    - 看到 sRGB/DCI-P3/...target 但 triangle 很小 → probe 对准屏幕中心
#    - Δu'v'(W) > 0.01 → 屏幕色温没设 D65
#    - Max ΔE2000 > 5 → 探针 OETF 与屏幕 OETF 不匹配 / OSD 设错
```
