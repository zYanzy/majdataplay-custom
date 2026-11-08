# MajdataPlay 自定义补丁与传感器配色工具

针对 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) Windows 运行包（2.0.3 / Mono 后端）的非官方定制：一个外置 GUI 工具，用于直接修改「调试 → 显示传感器」触发态的高亮颜色；以及配套的 IL 补丁引擎，可在颜色补丁之外同时注入 Fault 计数器。

本仓库只包含自行编写/修改的代码与文档，**不含**游戏本体、官方二进制、谱面、皮肤、成绩库或个人设置。

## 功能

- **Fault 计数器**：`BGInfoOption` 追加 `Fault = 16`；`ObjectCounter` 增加 `long _fault`（初始 0），Miss / Good / 六个 Great 判定时 -1（不含 Perfect 与 TooFast），并输出到信息显示位。设置中把 `BGInfo` 设为 `Fault` 即可显示。
- **传感器高亮任意配色**：`DummyTouchPanelRenderer.OnPreUpdate()` 触发态原本是灰色 `Color(0, 0, 0, 0.3)`，工具可改成任意 RGBA（示例为红色 `Color(1, 0, 0, 0.3)`）；空闲态保持全透明。
- **幂等补丁引擎**：输入官方原版 DLL 会注入全部补丁；输入已打过补丁的 DLL 只改颜色。定位颜色不依赖 `0.3f` 常量，而用 `brtrue.s` 的跳转目标作语义锚点。
- **GUI**：预设色板 / RGBA 滑块 / HEX / 系统取色器 / 传感器面板实时预览 / 一键应用 / 还原默认 / 回滚官方原版 / 日志。

## 目录

| 路径 | 说明 |
| --- | --- |
| `sensor_color_tool.py` | tkinter GUI 主程序 |
| `engine-src/` | C# 补丁引擎源码（Mono.Cecil，Fault 注入 + 颜色修改） |
| `start.bat` / `启动配色工具.bat` | 启动脚本，自动探测带 tkinter 的 Python |
| `config.example.json` | 配置模板 |
| `restore.py` | 回滚示例脚本（需自备官方原版备份文件） |
| `dev-source-changes/` | 按 2.1.0 dev 分支结构修改的 10 个源码文件 |
| `LICENSE` | GPLv3 全文，来自官方仓库 |
| `INSTALL.md` | 在另一台同版本客户端上部署的完整指南（供 AI Agent 阅读执行） |

## 环境与首次构建

- Windows + Python 3（需带 tkinter，安装时勾选 *tcl/tk and IDLE*）
- .NET 10 SDK（`engine-src/patcher.csproj` 目标框架 `net10.0`，依赖 Mono.Cecil 0.11.6）

```bat
cd engine-src
dotnet build -c Release -o ../engine
cd ..
copy config.example.json config.json
```

编辑 `config.json`，把 `game_dir` 指向自己的 `MajdataPlay_Build-LATEST` 目录，然后双击 `启动配色工具.bat`（或 `start.bat`）。Git Bash 下把 `copy` 换成 `cp`。

> 「回滚官方原版」按钮需要自备官方原版 `Assembly-CSharp.dll`，放到仓库上一级的 `fault_patch_backup/Assembly-CSharp.dll`；`restore.py` 同理，可用 `--backup` 指定备份目录。本仓库不分发任何游戏二进制。

## 注意事项

- **MajdataHub 官方增量更新会覆盖 `Assembly-CSharp.dll`，补丁会丢失**；更新后打开工具点一次「应用到游戏」即可整套恢复。
- 工具只替换程序文件，绝不覆盖 `MaiCharts/`、`Skins/`、`*.db`、`settings.json`；每次应用前自动备份当前 DLL 到 `backups/`。
- 颜色改动需重新加载程序集，工具会自动关闭并重启游戏。
- `dev-source-changes/` 对应 **2.1.0 dev 分支**结构，**不要**用它编译出的 DLL 覆盖 2.0.3 运行包；两者的语言包格式与设置枚举也不同。
- 运行包的语言包由 `Resources.Load<TextAsset>("Langs/<code>")` 读取，修改 `StreamingAssets/Langs/*.json` 在运行时无效。
- 信息项放在 `SecondaryBGInfo` 时须把 `EnableJudgeTimingGauge` 设为 `false`，否则初始化阶段会被强制改为 `BGInfoOption.None` 而不显示。

## 许可与声明

本项目是对 GPLv3 项目 MajdataPlay 的非官方修改。`LICENSE` 为官方 GPLv3 全文，`dev-source-changes/` 中对官方源码的修改同样按 GPLv3 提供；本仓库其余原创内容也按 GPLv3 发布。第三方依赖 [Mono.Cecil](https://github.com/jbevain/cecil) 为 MIT。

本仓库与 MajdataPlay 官方无关联。以 IL 补丁修改游戏程序集可能不符合相关使用条款，请自行评估；使用前请备份，作者不对数据丢失或账号问题负责。
