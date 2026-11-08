# MajdataPlay 自定义补丁 —— 已拆分为两个独立仓库（归档）

> **本仓库不再维护功能代码**，但它仍是**整个项目的入口**：[`DEVELOPMENT.md`](DEVELOPMENT.md) 是给 AI Agent 读的开发交接文档（项目结构、技术路线、工具链、代码资产地图、新功能开发 SOP、全部已知坑），也是本仓库唯一持续维护的文档。要**开发新功能**请先读它；要**安装已有功能**请前往下面两个仓库。
>
> 原先的两个功能已拆成两个彼此独立、可单独安装的公开仓库：

| 想要的功能 | 仓库 | 说明 |
| --- | --- | --- |
| **Fault 计数器** | [zYanzy/majdataplay-fault-counter](https://github.com/zYanzy/majdataplay-fault-counter) | 新增信息显示项 `Fault`，Miss / Good / Great 时 -1；命令行一键应用 + 完整校验 |
| **传感器高亮配色** | [zYanzy/majdataplay-sensor-color](https://github.com/zYanzy/majdataplay-sensor-color) | tkinter GUI 把「调试 → 显示传感器」的灰色高亮改成任意 RGBA |

两份补丁针对同一个游戏版本：**MajdataPlay Windows 运行包 2.0.3 `20260917-637bba0`（Mono 后端）**。它们分别只改 `BGInfoOption`/`ObjectCounter` 与 `DummyTouchPanelRenderer`，**互不依赖、互不覆盖**，可以只装一个，也可以按任意顺序两个都装。

## 为什么要拆

原来的合集版把两件事塞进同一个补丁引擎：一次运行会同时注入 Fault 计数器和改写高亮颜色，因此无法"只装其中一个"。拆分后：

- 两个引擎各只有一个职责，`--inspect` 也只校验自己的 IL 锚点；
- 各仓库有独立的 README / INSTALL.md / 回滚脚本，给别的电脑或 AI Agent 部署时目标明确；
- 官方 MajdataHub 更新覆盖 `Assembly-CSharp.dll` 后，各自重新应用即可，不会互相牵连。

## 三份文档的分工

| 想做的事 | 读哪份 |
| --- | --- |
| **开发一个新功能**（写新的 IL 补丁引擎） | 本仓库 [`DEVELOPMENT.md`](DEVELOPMENT.md) |
| 安装已有的某个功能 | 对应功能仓库的 `INSTALL.md` |
| 了解拆分背景、找历史快照 | 本页 |

`DEVELOPMENT.md` 说明：为什么这个项目走 IL 补丁而不是 Unity Build、需要哪些工具、两个现有引擎分别示范了什么、新功能从可行性判断到交叉验证再到分发的完整 SOP，以及全部已知坑。**只凭一个同版本运行包就能开发**，不需要 `MajdataPlay-dev` 源码工程。

## 本仓库剩余内容

这里保留的是**拆分前的历史快照**，仅供追溯（外加唯一持续维护的 `DEVELOPMENT.md`）：

- 合集版补丁引擎源码、GUI、dev 源码改动、语言包修改；
- 旧版 `INSTALL.md`（已改为跳转到新仓库）；
- [`DEVELOPMENT.md`](DEVELOPMENT.md)：项目交接 / 开发指南，**持续维护**；
- GPLv3 `LICENSE`。

这些文件**不再接收更新，也不建议用于新部署**。新部署请一律使用上面两个新仓库；两者都含完整的 `INSTALL.md`，可直接交给 AI Agent 执行。

> 本机仍在使用的 GUI 已升级为「只改颜色」的独立引擎：它不会注入也不会移除 Fault 计数器。因此若游戏更新程序被官方覆盖，需要**分别**重新应用两个补丁：先跑 `majdataplay-fault-counter` 的 `apply_fault.py`，再打开配色工具点「应用到游戏」。

## 许可与声明

本项目是对 GPLv3 项目 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的非官方修改。`LICENSE` 为官方 GPLv3 全文，`dev-source-changes/` 中对官方源码的修改同样按 GPLv3 提供；本仓库其余原创内容也按 GPLv3 发布。第三方依赖 [Mono.Cecil](https://github.com/jbevain/cecil) 为 MIT。

本仓库与 MajdataPlay 官方无关联。以 IL 补丁修改游戏程序集可能不符合相关使用条款，请自行评估；使用前请备份，作者不对数据丢失或账号问题负责。
