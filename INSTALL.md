# 本指南已迁移（归档仓库）

> **请勿按本文件部署。** 这个仓库是拆分前的历史快照，本指南描述的「一次性注入 Fault 计数器 + 高亮颜色」合集补丁已被两个独立仓库取代。

请按需要打开对应仓库里的 `INSTALL.md`，并严格按其中步骤执行：

| 目标 | 仓库 | 直链 |
| --- | --- | --- |
| 只装 **Fault 计数器** | `majdataplay-fault-counter` | https://github.com/zYanzy/majdataplay-fault-counter/blob/main/INSTALL.md |
| 只装 **传感器配色工具** | `majdataplay-sensor-color` | https://github.com/zYanzy/majdataplay-sensor-color/blob/main/INSTALL.md |
| 两个都要 | 按任意顺序分别执行上面两份指南 | — |

两份指南都包含：版本指纹与兼容性校验、备份、构建、`--inspect` 校验、只替换 `Assembly-CSharp.dll`、需要修改的 `settings.json` 键、验收、禁止事项与回滚。

## 给 AI Agent 的说明

- 如果你是被要求「clone 这个仓库然后按 INSTALL.md 部署」，**停下来**，向用户确认他到底要哪个功能，再改用上表对应的新仓库。
- 本目录里残留的 `engine-src/`、`sensor_color_tool.py`、`dev-source-changes/` 是旧合集版代码，**不要**用它们生成补丁，也不要用 `dev-source-changes/` 编译出的 DLL 覆盖 2.0.3 运行包。
- 两个新引擎的 `--inspect` 输出格式不同：Fault 仓库为 `{"hasFault":bool}`，配色仓库为 `{"r":..,"g":..,"b":..,"a":..}`。旧合集版输出同时包含两组字段，已不再使用。
- 硬性前提不变：MajdataPlay **Windows 运行包 2.0.3 `20260917-637bba0`（Mono 后端）**；官方原版 DLL 的 SHA256 为 `139817f1e0147f8fe21fdc6c0a24303f3a93c98a7fd049c4eebf54a09c875ad9`（1,241,600 字节）。
