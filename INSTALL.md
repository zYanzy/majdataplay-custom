# 部署指南：在另一台电脑上启用 Fault 计数器与传感器配色

> 本文件供 AI Agent 阅读执行。请严格按顺序操作，**任何一步校验失败就停止并向用户报告**，不要猜测、不要跳过、不要强行写入程序集。

## 1. 适用范围（硬性前提）

| 项目 | 要求 |
| --- | --- |
| 游戏 | MajdataPlay **Windows 运行包 2.0.3 `20260917-637bba0`，Mono 后端** |
| 目标文件 | `<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll` |
| 官方原版指纹 | SHA256 `139817f1e0147f8fe21fdc6c0a24303f3a93c98a7fd049c4eebf54a09c875ad9`，大小 `1,241,600` 字节 |

**版本不匹配必须停止。** IL 补丁的锚点（`BGInfoOption` 枚举、`ObjectCounter`、`DummyTouchPanelRenderer.OnPreUpdate` 里 `brtrue.s` 的跳转目标）绑定这一版程序集；其他版本（含 dev 分支 2.1.0）结构不同，强行注入会损坏程序集。**不要**拿 2.1.0 源码编译出的 DLL 覆盖 2.0.3 运行包，两者语言包格式与设置枚举也不同。

## 2. 依赖

- Windows
- Python 3（**必须带 tkinter**，用于 GUI）
- .NET 10 **SDK**（从仓库 clone 时需要 `dotnet build`）；若拿到的是已含 `engine/` 编译产物的完整目录，则只需 .NET 10 **运行时**
- git（可选）

## 3. 获取代码

**方式 A — 公开仓库（推荐）**

```bash
git clone https://github.com/zYanzy/majdataplay-custom.git
```

仓库**不含** `engine/`（编译产物被 `.gitignore` 排除），所以必须执行第 5 步构建。

**方式 B — 离线拷贝**

让用户把整个 `SensorColorTool/` 目录拷过来（含 `engine/patcher.dll` 和 Mono.Cecil 的 DLL），可跳过第 5 步。

下文用 `<TOOL_DIR>` 表示该目录。

## 4. 定位游戏目录、关闭游戏、备份

1. 找到含 `MajdataPlay.exe` 的目录，记为 `<GAME_DIR>`。**向用户确认，不要猜**。
2. 确认游戏已完全退出；DLL 被占用时不能替换。
3. **先做两份备份**（替换前必做）：
   - `<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll` → `<TOOL_DIR>\..\fault_patch_backup\Assembly-CSharp.dll`（供 GUI 的「回滚官方原版」用）
   - 再留一份带时间戳的副本，例如 `<TOOL_DIR>\backups\Assembly-CSharp_<日期>.dll`
4. 可选校验：计算该 DLL 的 SHA256。若等于第 1 节的指纹，确认是同一官方原版；若不同，以第 5 步的 `--inspect` 结果为准。

## 5. 构建补丁引擎（方式 A 必做）

```bash
cd <TOOL_DIR>\engine-src
dotnet build -c Release -o ..\engine
```

依赖 NuGet 包 `Mono.Cecil 0.11.6`。若因无 SDK / 无网络构建失败，改用方式 B 获取已编译的 `engine/`，不要自行改写 csproj 的目标框架。

## 6. 兼容性校验（写入前必做）

```bat
set "DLL=<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll"
dotnet "<TOOL_DIR>\engine\patcher.dll" "%DLL%" --inspect
```

期望输出**一行 JSON**：

- 官方原版：`{"hasFault":false,"r":0,"g":0,"b":0,"a":0.3}`
- 已打补丁：`{"hasFault":true,"r":1,"g":0,"b":0,"a":0.3}`（数值可能不同）

**只有成功输出 JSON 才能继续**。抛异常、找不到方法、返回码非 0 → 版本不兼容，立即停止并报告。

## 7. 生成并写入补丁

颜色为 `R,G,B,A`，各分量 0..1；默认红色 `1,0,0,0.3`。

```bat
mkdir "<TOOL_DIR>\output" 2>nul
dotnet "<TOOL_DIR>\engine\patcher.dll" "%DLL%" "<TOOL_DIR>\output\Assembly-CSharp.patched.dll" --color 1,0,0,0.3
```

预期输出 `OK hasFault=injected`（原版）或 `OK hasFault=existing`（已补丁）。引擎是**幂等**的：输入官方原版会注入完整 Fault 计数器 + 颜色；输入已补丁 DLL 则只改颜色、保留已有补丁。

二次校验生成物，要求 `hasFault=true` 且 RGBA 与目标一致：

```bat
dotnet "<TOOL_DIR>\engine\patcher.dll" "<TOOL_DIR>\output\Assembly-CSharp.patched.dll" --inspect
```

然后**只替换这一个文件**：

```bat
copy /Y "<TOOL_DIR>\output\Assembly-CSharp.patched.dll" "%DLL%"
```

（Git Bash 下用 `cp -f`。）最后再对 `%DLL%` 执行一次 `--inspect` 确认。

## 8. 写工具配置

把 `config.example.json` 复制为 `config.json` 并编辑：

```json
{
  "auto_start_game": false,
  "game_dir": "<GAME_DIR>"
}
```

`game_dir` 中 Windows 反斜杠在 JSON 里要写成 `\\`。**Agent 环境下建议 `auto_start_game: false`** —— 由命令会话启动的游戏进程常被系统回收，让用户手动双击 `MajdataPlay.exe` 最可靠。

## 9. 让功能在游戏里可见（改 `settings.json`，改前先备份）

`<GAME_DIR>\settings.json` 中只改这几个键，其余内容一律保留：

| 路径 | 值 | 说明 |
| --- | --- | --- |
| `Game.BGInfo` | `"Fault"` | 推荐放主显示位 |
| `Game.SecondaryBGInfo` | `"None"` | 也可设为 `"Fault"`，但见下行 |
| `Game.EnableJudgeTimingGauge` | `false` | **若 Fault 放在 `SecondaryBGInfo`，此项必须为 false**，否则初始化时副显示位被强制改为 `None`，设置里选中也不显示 |
| `Debug.DisplaySensor` | `true` | 否则看不到传感器高亮 |

设置项名在中文界面可能显示英文 `Fault`（语言包打包在 `resources.assets`，本补丁不改），HUD 标题硬编码 `Fault`，功能不受影响。

## 10. 验收

1. 启动游戏、进入任意谱面、开启「调试 → 显示传感器」：触发时高亮为目标色，空闲时全透明。
2. 信息位显示 `Fault`，初始 0，出现 GREAT / GOOD / MISS 时每次 -1（不含 Perfect 与 TooFast）。
3. 日志中无 `InvalidProgramException` / `BadImageFormatException` / 程序集加载失败。
4. `MaiCharts/`、`Skins/`、`MajScores.db` 未被改动。

## 11. 禁止事项

- **只替换 `Assembly-CSharp.dll`**，绝不整目录覆盖；不要动 `MaiCharts/`、`Skins/`、`MajScores.db`、`Logs/` 及 `settings.json` 的其他键。
- 不要用 `dotnet build` / VS / MSBuild 编译游戏根目录的 `.sln` / `.csproj`（那些只给 IDE 提示用）。
- 不要在游戏运行时替换 DLL。
- 不要把任何游戏二进制、谱面、成绩库、个人设置上传或提交到版本库。
- **MajdataHub 官方增量更新会覆盖 `Assembly-CSharp.dll` 导致补丁丢失**；更新后重跑第 6–7 步，或打开 GUI 点一次「应用到游戏」即可整套恢复。

## 12. 回滚

用第 4 步备份的官方原版 DLL 覆盖回去；或在 GUI 点「回滚官方原版」（需要 `<TOOL_DIR>\..\fault_patch_backup\Assembly-CSharp.dll`）。

## 13. 日常使用

双击 `启动配色工具.bat`（或 `start.bat`）打开 GUI：预设色板 / RGBA 滑块 / HEX / 系统取色器 / 传感器面板实时预览 / 一键应用 / 还原默认 / 回滚官方原版 / 日志。颜色改动需重新加载程序集，工具会关闭并重启游戏（可用 `auto_start_game` 控制）。GUI 必须用 bat 启动，直接双击 `.py` 可能落到不带 tkinter 的解释器。
