# 用法

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

退出码：

| 退出码 | 含义 |
|---|---|
| 0 | 成功（含 `--dry-run`） |
| 1 | 运行失败（错误码出现在日志中：`NO_SAMPLES` / `MISSING_PRIMARIES` / `SDK_*` / `INVALID_PLAN` 等） |
| 2 | 计划解析失败 |
| 99 | 未捕获异常 |

## TestPlan 字段

完整示例见 `recipes/color-space-coverage-srgb.json`。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 计划名（出现在报告中） |
| `connect` | string | 否 | `auto` 或 `usb`，默认 `auto` |
| `calibrateBeforeStart` | bool | 否 | true → 跑 plan 前先做 ZeroCal |
| `settleMs` | int | 否 | 贴色后等待时间（ms），默认 1500 |
| `targets` | string[] | 否 | 目标色域名数组，默认 `["sRGB"]`。支持：`sRGB` / `DCI-P3` / `AdobeRGB` / `Rec.2020` |
| `patches` | object[] | 是 | 至少包含 `id` 为 `red` / `green` / `blue` / `white` 的四个 patch |
| `pass` | object | 否 | 通过条件：`max_delta_e2000` + `min_coverage`，缺省则该项不参与判定 |

patch 字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | 是 | 唯一 id；色域分析要求至少存在 `red` / `green` / `blue` / `white` |
| `hex` | string | 是 | 颜色 `#RGB` 或 `#RRGGBB` |
| `avg` | int | 否 | SDK averaging count，默认 8 |
| `label` | string | 否 | 备注 |

## 常见运行模式

```powershell
# 1) 冒烟：只解析 plan
color-measure --plan my.json --dry-run

# 2) 真实测量（CA-410 USB 已接 + 显示器对准 + CA-S40 主程序已关）
color-measure --plan my.json --out runs\2026-09-10

# 3) CI 友好的退出码
$code = (Start-Process color-measure -ArgumentList '--plan','ci.json','--out','runs/ci' -Wait -PassThru).ExitCode
if ($code -ne 0) { throw "QA failed ($code)" }
```

## SDK 错误码速查

| rc | 含义 | 可能原因 |
|---|---|---|
| 426 | `--Measurement fail.` | 探针未对准 patch / 显示器色彩增强未关 / HDR / 护眼模式 |
| 其他 | 见 `native/SDK/CASDK2ErrorDefine.h` 的对照表 |

CLI 输出的 `[Code]` 字段就是 SDK 的 rc，或 `SDK_EXCEPTION` / `SDK_READ` / `NO_SAMPLES` / `MISSING_PRIMARIES` / `INVALID_PLAN` 之一。
