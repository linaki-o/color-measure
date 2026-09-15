# 色彩科学背景

工具用到的所有指标的概念、来源、判读。

## Coverage

**定义**：`coverage = clamp(measured_area / target_area, 0, 1)`

`measured_area` 和 `target_area` 都是 CIE 1931 xy 图上 R/G/B 三点构成三角形的面积（用 Shoelace 公式计算，见 `src/ColorMeasure.Application/Analysis/PolygonMath.cs`）。

**含义**：实测三原色包住的标准色域比例。

**判读**：

| Coverage | 含义 |
|---|---|
| 1.00 | 完美——实测色域 ≥ 标准色域 |
| 0.90 - 0.99 | 几乎覆盖，少数边缘色略不足 |
| 0.70 - 0.89 | 明显缺口（看图找三角形哪个角缩了） |
| < 0.70 | 严重缺口 / 测错 |

**注意**：Coverage 只看面积，**不看位置**。三角形可以旋转/平移但面积不变，Coverage 一样。看原色位置偏移请用 `Max ΔE2000`。

源码：`src/ColorMeasure.Application/Analysis/ColorAnalyzer.cs:21-23, 41-44`

## Max ΔE2000

**定义**：实测 R / G / B 三个原色到标准原色的 [CIEDE2000](https://en.wikipedia.org/wiki/Color_difference#CIEDE2000) 距离的最大值。

CIEDE2000 是 CIE 142-2001 推荐的色差公式，比 ΔE76 / ΔE94 更接近人眼感知。在 L\*a\*b\* 空间考虑亮度/饱和度/色调三个分量 + 交叉项。本工具用 ~80 行的纯 C# 实现（`Ciede2000.cs`），通过 38/43 个 Sharma 2005 测试向量。

**判读**：

| Max ΔE2000 | 含义 |
|---|---|
| ≤ 1 | 优秀——肉眼几乎不可分辨 |
| 1 - 2 | 良好——专业显示器水平 |
| 2 - 5 | 可接受——消费级显示器正常水平 |
| 5 - 10 | 偏大——可能 ICC profile 不对 / 屏幕没校准 |
| > 10 | 严重——很可能 OSD 设错 / 探针 OETF 失配 |

**与 Coverage 的关系**：两个独立维度。一台原色 xy 偏的屏幕可能 Coverage 仍接近 1（看 sRGB R 略偏，Coverage 仍大）；一台三角形小但 xy 准的屏幕 Coverage 低但 ΔE2000 小。两者都要看。

**注意**：CIEDE2000 的"完美匹配"对屏幕测量来说**不现实**——原色 xy 漂移 0.005 就够产生 ΔE2000 ≈ 2。显示器面板制造公差本身就允许 ±0.005 的 xy 漂移。

源码：`src/ColorMeasure.Application/Analysis/Ciede2000.cs`, `ColorAnalyzer.cs:47-52`

## Δu'v' (W)

**定义**：

$$u' = \frac{4x}{-2x + 12y + 3}, \quad v' = \frac{9y}{-2x + 12y + 3}$$

$$\Delta u'v' = \sqrt{(u'_m - u'_t)^2 + (v'_m - v'_t)^2}$$

CIE 1976 UCS (Uniform Chromaticity Scale) 是 1976 年发布的对 CIE 1931 xy 图的修正，让图上相同欧式距离尽量接近人眼的"感知等距"。`(x, y) → (u', v')` 是**线性分式变换**（projective），不是普通缩放。

**判读**：

| Δu'v' | 色温差（近似） | 含义 |
|---|---|---|
| ≤ 0.005 | ≤ 50K | 极好 |
| 0.005 - 0.010 | 50-200K | 良好 |
| 0.010 - 0.020 | 200-500K | 色温有偏差 |
| > 0.020 | > 500K | 显著偏离 D65 |

1000K 偏差大约对应 Δu'v' = 0.01。`Δu'v'(W)` 是**色温是否正确**的最直接指标。

**与色温的关系**：

- 显示器色温设 6500K (D65) → Δu'v' ≈ 0
- 7500K（偏冷） → Δu'v' ≈ +0.020（往蓝偏）
- 5000K（偏暖） → Δu'v' ≈ -0.010（往红偏）

**与原色 R/G/B 无关**：色温只改 R/G/B 的相对电流，不改每个原色单独的色度。所以改色温 → `Δu'v' (W)` 变但 Coverage / Max ΔE2000 几乎不变。

源码：`src/ColorMeasure.Domain/Entities/ChromaticityPoint.cs:15-23`, `src/ColorMeasure.Application/Analysis/ColorAnalyzer.cs:50-51`

## Gamma

**定义**：在归一化输入 V ∈ [0, 1] 和归一化亮度 Y ∈ [0, 1] 之间拟合

$$Y = a \cdot V^\gamma$$

通过对数空间最小二乘：

$$\ln Y = \gamma \cdot \ln V + \ln a$$

**v0+ 流程**：

1. 从 samples 中提取灰阶 patch（R = G = B 的输入）
2. 用 `Y_norm = (Lv - black) / (white - black)` 归一化（v0+ 的"v0+"是直接用原始 Lv——黑场偏置会偏 γ，但实测显示器黑场很小，影响可控；future v1+ 会用非线性最小二乘同时拟合 black）
3. `MinSamples = 2` 个有效（v > 0 且 Lv > 0）点就够
4. log-log OLS 拟合：斜率 = γ，截距 = ln(a)，R² = 1 - SS_res/SS_tot

**判读**：

| γ | 含义 |
|---|---|
| 1.0 | 线性（早期 Mac/Windows 默认） |
| 1.8 | 旧 Apple 显示器标准 |
| 2.0 | 简单 gamma |
| 2.2 | sRGB / Rec.709 标准（典型显示器默认） |
| 2.4 | BT.1886（带黑场补偿的现代标准） |
| > 2.4 | OETF 偏陡，亮调太暗 |
| < 1.8 | OETF 偏平，亮调太亮 |

R² < 0.95 表明显示器 OETF 不是单纯的 power law（通常是有局部 sRGB 拐点），或测量受噪声影响。

源码：`src/ColorMeasure.Application/Analysis/GammaAnalyzer.cs`

### 关键实现选择

- **不减去 blackLv**——初版减了，把 γ=2.2 拟合到 γ=2.74。原因是 `Lv - b` 不是 `a·V^γ` 的形式（off-lift 会扭曲 log-log 关系）
- **输入>0 和 Lv>0 才用**——log(0) 未定义，v=0 和 Lv=0 都跳过
- **3 点 v.s. 11 点**：3 点（v0 默认 gray-25/50/75）能给出粗略 γ，11 点（`gamma-ramp.json`）给出 R² > 0.99 的稳定拟合

## 色温

**原理**：屏幕的"白点"是 R/G/B 三个子像素的混合。色温 = 调不同的 R/G/B 相对电流，使混合出的白光在不同色度点上。

| 显示器 OSD 模式 | 实际白点 xy | Δu'v' (W) |
|---|---|---|
| D65 / 6500K (sRGB) | (0.3127, 0.3290) | 0 |
| 7500K | (0.2990, 0.3390) | ≈ 0.020 |
| 5000K (D50) | (0.3457, 0.3585) | ≈ 0.040（且 D50 不是 sRGB 标准，sRGB target 必报 FAIL）|

**色温 vs 色准**：色温只约束 R+G+B 混合出来的白点，**不约束每个 R/G/B 单独位置**。所以：
- 色温准了 R/G/B 不一定准
- R/G/B 准了色温也不一定准

**色温对测量的影响**：

| 指标 | 影响 |
|---|---|
| **Δu'v' (W)** | **直接受影响**——色温设错这一列就高 |
| **R/G/B 原色 xy** | **不受影响**——原色色度由背光+滤色片决定，与白平衡无关 |
| **Coverage** | **不受影响**——R/G/B 三点不变则面积不变 |
| **Max ΔE2000** | **不受影响**——原色 xy 不变则 dE 不变 |
| **绝对 Lv** | **可能受影响 ±20%**——不同色温对 RGB 子像素功率要求不同 |
| **Gamma 形状** | **不受影响**——Gamma 描述输入-亮度响应的形状 |

**实操建议**：
1. **报告存证时记下当时屏幕的色温预设值**——Δu'v' (W) 是和 D65 比的，换台机器/色温就不能直接横向比
2. **关掉显示器侧的所有"色温自动"模式**（常见的"EyeCare / 护眼 / 暖色"）再测
3. **OETF 校准**（把屏幕 gamma 校到 2.2）也跟色温正交，但通常 OETF 工具（Windows ICC、MadVR 等）会同时把白点设到 D65

**只用 Δu'v' (W) 诊断色温**——`Max ΔE2000` 和 `Coverage` 看不出色温问题。

## CIE 1931 / 1976 UCS 的来源

- **CIE 1931 (x, y)**：CIE Proceedings (1928), 1931。MacAdam 1942 椭圆指出其不均匀性（20:1 椭圆大小比）
- **CIE 1976 UCS (u', v')**：CIE 015:1986 / 015:2018，把均匀性改善到 4:1
- **CIE ΔE2000 (CIEDE2000)**：CIE 142:2001，1:1.6:1（×C×h 三个方向）
- **Wyszecki & Stiles** "Color Science"（2000, Wiley）——业界圣经，3.3.9 节给出 u'v' 公式

`cie1931xyz.png`（仓库根目录）是 Zhihu 上 2026 公开的参考图，本工具的马蹄填充 + wavelength tick + 10×10 网格都按它的画法。
