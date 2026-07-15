# maimai-chart-engine（vendored）

- 来源仓库：https://github.com/Lxns-Network/maimai-prober-frontend
- 来源路径：`packages/maimai-chart-engine`
- 固定提交：`ad801c78413a9a7470355cb17dd2132a7015ca47`（2026-07-07）
- 引入日期：2026-07-15
- 许可证：MIT，版权归 Lxns-Network 所有（全文见同目录 LICENSE，复制自上游仓库根目录）

本目录内除 LICENSE 与本文件外，所有文件均为上述提交中 `packages/maimai-chart-engine` 目录的逐字节未修改副本，仅有一处调整：删除了上游的 `tsconfig.json` 存根（其 `extends` 指向上游 monorepo 根目录，在本仓库中悬空导致 vite 构建失败；本目录代码改由仓库根 tsconfig 约束）。

补充说明：

- 唯一外部运行时依赖为 `ts-pattern`（见 package.json）。
- 渲染与音频默认引用静态资源 `/assets/maimai/chart/sensor.webp` 与 `/assets/maimai/chart/answer.wav`，可分别通过 `sensorImagePath` / `answerSoundPath` 配置项覆盖。
