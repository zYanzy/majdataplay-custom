# MajdataPlay 自定义补丁 —— 已拆分为两个独立仓库（归档）

> **本仓库不再维护。** 原先的两个功能已拆成两个彼此独立、可单独安装的公开仓库。请按需要前往：

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

## 本仓库剩余内容

这里保留的是**拆分前的历史快照**，仅供追溯：

- 合集版补丁引擎源码、GUI、dev 源码改动、语言包修改；
- 旧版 `INSTALL.md`（已改为跳转到新仓库）；
- GPLv3 `LICENSE`。

这些文件**不再接收更新，也不建议用于新部署**。新部署请一律使用上面两个新仓库；两者都含完整的 `INSTALL.md`，可直接交给 AI Agent 执行。

> 本机仍在使用的 GUI 已升级为「只改颜色」的独立引擎：它不会注入也不会移除 Fault 计数器。因此若游戏更新程序被官方覆盖，需要**分别**重新应用两个补丁：先跑 `majdataplay-fault-counter` 的 `apply_fault.py`，再打开配色工具点「应用到游戏」。

## 许可与声明

本项目是对 GPLv3 项目 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的非官方修改。`LICENSE` 为官方 GPLv3 全文，`dev-source-changes/` 中对官方源码的修改同样按 GPLv3 提供；本仓库其余原创内容也按 GPLv3 发布。第三方依赖 [Mono.Cecil](https://github.com/jbevain/cecil) 为 MIT。

本仓库与 MajdataPlay 官方无关联。以 IL 补丁修改游戏程序集可能不符合相关使用条款，请自行评估；使用前请备份，作者不对数据丢失或账号问题负责。
