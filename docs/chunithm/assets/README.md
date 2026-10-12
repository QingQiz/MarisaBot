# CHUNITHM 等级与状态素材

## 来源记录

[`rank-native.json`](rank-native.json) 保存等级素材的原始研究记录，包括游戏版本、DDS 来源及 SHA-256、字片裁切坐标、原生尺寸、透明度边界和字距。来源为 `SDHD 2.45.00 / CHUNITHM X-VERSE-X` 中的 `data/surfboard/texture/CHU_UI_Result_Rank_00.dds`。此文件仅用于来源追踪与复核，运行时代码不会读取它。

该记录从前端 `public` 目录移至此处，内容逐字节保留；文件 SHA-256 为 `db409bba10dc5f12bbcfaa0512314748c544d214ff57a99208fc977805619228`。

## 等级 SVG

运行时文件位于 `Marisa.Frontend/public/assets/chunithm/pic/rank_*.svg`，由 `src/components/chunithm/utils/score.ts` 的 `rankIcon()` 生成 `/assets/chunithm/pic/rank_${rank}.svg` URL；等级名转成小写，`null` 等级默认使用 `d`。

14 个等级 `d`、`c`、`b`、`bb`、`bbb`、`a`、`aa`、`aaa`、`s`、`sp`、`ss`、`ssp`、`sss`、`sssp` 均使用 **544 × 226** 的画布和 `viewBox`。各等级组合使用未缩放的原生字片，整体标签（包含 `+`）在画布内居中，不逐等级裁掉透明边缘。`p` 表示 `+`。

所有含 `S` 的等级复用相同的 **168 × 194** 单 `S` 字片，嵌入 PNG 的 SHA-256 均为 `9571d6e290910a13fd707e732ca631103047524f269dd6742b24eb7f68808f8a`；多 `S` 与附加 `+` 不改变单 `S` 的原生尺寸。旧 `rank_*.png` 保留，但当前 `rankIcon()` 使用 SVG。

## 对战状态素材

运行时 PNG 同样位于 `Marisa.Frontend/public/assets/chunithm/pic/`，由 `src/components/chunithm/partial/VersusMarks.vue` 选择家族和状态。

| 家族 | URL 前缀 | 原生尺寸 | 使用页面 |
| --- | --- | --- | --- |
| Result | `/assets/chunithm/pic/versus_` | combo / chain：276 × 48；clear：272 × 44 | 双人单曲 `ChuVersus.vue`、双人批量 `ChuVersusBatch.vue` |
| Common | `/assets/chunithm/pic/versus_common_` | 所有状态：134 × 26 | 多人单曲 `ChuVersusMulti.vue`、多人批量 `ChuVersusMultiBatch.vue` |

Common 使用自己的原生字片和布局，并非将 Result 大图简单缩小。原生尺寸是素材来源信息；显示端通过 `VersusMarks.vue` 的 `mark-slot` 与 `--chu-mark-width` 统一同一家族各状态的展示尺寸，不按每张 PNG 的固有尺寸分别排版。Result 的状态槽比例为 `276 / 48`，Common 为 `134 / 26`。

`VersusMarks` 的 `family` 默认是 `result`，双人页面沿用默认值；多人页面显式传入 `family="common"`。两套家族都提供 `fullcombo`、`alljustice`、`alljusticecritical`、`fullchain`、`fullchain2`、`clear`、`hard`、`brave`、`absolute`、`catastrophy`。Common 另有 `failed` PNG；Result 的 `failed` 使用文字显示。

## 构建与发布

Vite 配置没有关闭 `copyPublicDir`，构建会将 `public` 下的文件复制到 `Marisa.Frontend/dist`。`Marisa.StartUp.csproj` 在构建和发布后将 `dist/**` 复制到对应输出的 `wwwroot`。因此来源记录放在 `docs`，不会随前端公共资源进入运行时输出。

`vite.config.ts` 使用 `emptyOutDir: false`，启动项目的复制目标也不会删除旧文件。移动来源记录后，已有 `dist/assets/chunithm/pic/rank-native.json` 或输出 `wwwroot/assets/chunithm/pic/rank-native.json` 可能仍在，需要仅删除对应的旧 JSON；保留等级 PNG、状态纹理和其他输出文件。
