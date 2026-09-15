# Recipe 写作指南

写一份好的 recipe 是测准的关键。三类坑：
1. **GPU palette 窗口没在探针视野里** → 测到的是屏幕当前显示的内容（可能是桌面壁纸）
2. **屏幕色温/ICC/OETF 不在标准状态** → Δu'v' (W) 和 Max ΔE2000 一起偏高，分不清是谁的问题
3. **灰阶不够** → Gamma 拟合 R² 低

下面给 3 套覆盖典型场景的 recipe，外加写作 checklist。

## 仓库自带的 2 套

### `color-space-coverage-srgb.json` — 默认，色域 QA

```json
{
  "patches": [
    { "id": "white",  "hex": "#FFFFFF", "avg": 8 },
    { "id": "red",    "hex": "#FF0000", "avg": 8 },
    { "id": "green",  "hex": "#00FF00", "avg": 8 },
    { "id": "blue",   "hex": "#0000FF", "avg": 8 },
    { "id": "gray-25", "hex": "#404040", "avg": 4 },
    { "id": "gray-50", "hex": "#808080", "avg": 4 },
    { "id": "gray-75", "hex": "#C0C0C0", "avg": 4 }
  ]
}
```

- 4 个原色 → 4 个色域的 coverage + ΔE2000
- 3 步灰阶（25/50/75%）→ 粗略 gamma 估计（R² 大概 0.90-0.95）
- `avg=8` 给原色 / `avg=4` 给灰阶（灰阶信号弱，平均少次避免长时间挡住屏幕）

适合：日常色域 QA / 第一次接入新显示器 / 验证 ICC profile 改完有没有效果。

### `gamma-ramp.json` — 专门测 Gamma

```json
{
  "patches": [
    { "id": "#000000", "hex": "#000000", "avg": 8 },
    { "id": "#191919", "hex": "#191919", "avg": 8 },
    { "id": "#333333", "hex": "#333333", "avg": 8 },
    { "id": "#4d4d4d", "hex": "#4d4d4d", "avg": 8 },
    { "id": "#666666", "hex": "#666666", "avg": 8 },
    { "id": "#808080", "hex": "#808080", "avg": 8 },
    { "id": "#999999", "hex": "#999999", "avg": 8 },
    { "id": "#b3b3b3", "hex": "#b3b3b3", "avg": 8 },
    { "id": "#cccccc", "hex": "#cccccc", "avg": 8 },
    { "id": "#e6e6e6", "hex": "#e6e6e6", "avg": 8 },
    { "id": "#ffffff", "hex": "#ffffff", "avg": 8 },
    { "id": "red",   "hex": "#FF0000", "avg": 8 },
    { "id": "green", "hex": "#00FF00", "avg": 8 },
    { "id": "blue",  "hex": "#0000FF", "avg": 8 },
    { "id": "white", "hex": "#FFFFFF", "avg": 8 }
  ]
}
```

11 步灰阶（hex 格式触发 gamma 拟合）→ 稳定 R² > 0.99 + 准确的 γ / a / Black/White cd/m²。
4 个原色是报告色域的需要，**不是** gamma 拟合需要的。

适合：测 OETF 校准、找显示器 gamma 拐点、做 sRGB <-> BT.1886 转换。

## 模板：单通道 ramp

要测 R / G / B 各自的 gamma 通道（看 channel balance / grayscale tinting）：

```json
{
  "name": "Per-Channel-Gamma",
  "settleMs": 1500,
  "targets": ["sRGB"],
  "patches": [
    { "id": "r-100",  "hex": "#FF0000" }, { "id": "r-75",  "hex": "#BF0000" },
    { "id": "r-50",  "hex": "#800000" }, { "id": "r-25",  "hex": "#400000" },
    { "id": "g-100", "hex": "#00FF00" }, { "id": "g-75",  "hex": "#00BF00" },
    { "id": "g-50",  "hex": "#008000" }, { "id": "g-25",  "hex": "#004000" },
    { "id": "b-100", "hex": "#0000FF" }, { "id": "b-75",  "hex": "#0000BF" },
    { "id": "b-50",  "hex": "#000080" }, { "id": "b-25",  "hex": "#000040" },
    { "id": "white", "hex": "#FFFFFF" },
    { "id": "red",   "hex": "#FF0000" },
    { "id": "green", "hex": "#00FF00" },
    { "id": "blue",  "hex": "#0000FF" }
  ]
}
```

> v0+ 这套 channel ramp **不会**自动按通道分组成 3 个独立 gamma 报告——它走 gamma 拟合（识别 R=G=B）会失败（不是灰阶）。v1+ 可以加 `targetGammaByChannel: "rgb"` 选项。这套现在适合做 `samples.csv` + `summary.xlsx` 的细粒度诊断（手动 Excel 看），或者配合外部工具做单通道分析。

## 模板：色域对比（A vs B）

```json
{
  "name": "Compare-sRGB-vs-P3",
  "settleMs": 1500,
  "targets": ["sRGB", "DCI-P3"],
  "patches": [
    { "id": "white", "hex": "#FFFFFF" },
    { "id": "red",   "hex": "#FF0000" },
    { "id": "green", "hex": "#00FF00" },
    { "id": "blue",  "hex": "#0000FF" },
    { "id": "cyan",    "hex": "#00FFFF" },
    { "id": "magenta", "hex": "#FF00FF" },
    { "id": "yellow",  "hex": "#FFFF00" }
  ]
}
```

- 4 个原色 + 3 个二次色（CMY） → 7 个采样点足够区分 sRGB vs P3 主色差异
- 实际报出来的 P3 Coverage 应该明显 > sRGB Coverage（如果显示器是宽色域）

## Checklist：写 recipe 前

- [ ] 屏幕 OSD：色温设 D65/6500K，所有"EyeCare/护眼/暖色"关掉
- [ ] 屏幕 OSD：Gamma / EOTF 设 sRGB 或 2.2（不是 BT.1886 时改 recipe 期望值）
- [ ] ICC profile 加载你想要测的那份（系统默认 / Adobe RGB / 自定义）
- [ ] GPU palette 窗口 100% 覆盖屏幕（独占全屏、cursor 隐藏、Esc 才退）
- [ ] 探针 lens 紧贴屏幕中心（不能有空隙漏环境光）
- [ ] 室内环境光稳定（关窗帘/避开顶灯/避免日光直射探针）

## Checklist：recipe 写完后

- [ ] `--dry-run` 跑通（不接硬件，只解析）
- [ ] 至少包含 `white` / `red` / `green` / `blue` 4 个 patch
- [ ] 想测 gamma → 加 ≥ 3 步灰阶（hex 格式 `#404040` 或 `gray-NN` 名字）
- [ ] `targets` 列里是你想评估的色域
- [ ] `pass` 阈值合理：色域 QA 一般 `max_delta_e2000: 5`、`min_coverage: 0.90`；严格场景 `3` / `0.95`；广色域显示 P3/Rec.2020 建议放宽到 `8` / `0.70`

## 常见错误

| 现象 | 原因 | 修法 |
|---|---|---|
| `Max ΔE2000` 全部 > 30 | 探针 OETF 没设匹配 / OSD 设错色域模式 | 在 OSD 选"自定义色温 6500K"，ICC 选 sRGB 模式 |
| 4 个原色 xy 都聚成一点 | GPU palette 窗口没显示 / 屏幕没切换 patch | 看屏幕确认 patch 在切换；探针是不是完全在 palette 区域 |
| `Δu'v' (W)` > 0.02 + 4 个色域都 FAIL | 屏幕当前不是 D65（sRGB target）| OSD 改色温到 6500K / D65 模式 |
| Gamma 拟合 R² < 0.95 | 灰阶太粗 / 屏幕 OETF 复杂（有局部 sRGB 拐点）| 增加到 11 步 / 用 `gamma-ramp.json` |
| 全 0 个 sample 测到 | probe 没连接 / palette 窗口没切换 | 看 `rc=426` 日志；跑 `--dry-run` 先确认 JSON 解析 |

## 测一次需要多久

- 每 patch ~1.5s settle + ~0.5s measure（4-8 次平均）= ~3s
- 11 灰阶 + 4 原色 ≈ 50s
- 加 4 个色域分析 = ~1s
- 总共 ~1 分钟

CI/脚本里可以放心跑。
