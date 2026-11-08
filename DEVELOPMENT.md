# 开发指南：为 MajdataPlay 2.0.3 添加新的 IL 补丁功能

> **本文件供 AI Agent 在动手开发之前完整阅读。** 读完它，你应当能独立判断一个需求能不能做、如何定位 IL 锚点、如何写出一个与现有功能互不干扰的独立补丁，以及如何验证、部署和分发。
>
> **与 `INSTALL.md` 的区别**：`INSTALL.md` 是「安装已有补丁」，`DEVELOPMENT.md` 是「写新补丁」。两者不要混用。
>
> 本文不含任何游戏二进制、谱面、成绩库、个人路径或账号信息，可安全交给第三方 Agent。

---

## 0. 30 秒速览

| 项目 | 结论 |
| --- | --- |
| 目标游戏 | MajdataPlay **Windows 运行包 2.0.3 `20260917-637bba0`，Mono 后端** |
| 唯一可改的逻辑文件 | `<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll` |
| 技术路线 | `ilspycmd` 反编译 → `Mono.Cecil` 直接改 IL → 写回 DLL → 启动游戏验证 |
| **不需要** | Unity Editor、游戏源码工程、编译游戏 `.sln`、重打包 |
| 能做 | 托管 C# 逻辑：判定、计数、显示文本、颜色常量、条件分支、枚举值、字段 |
| 做不了 | prefab / 场景 / Shader / 贴图 / UI 层级 / 打包语言包 / 新增 MonoBehaviour 或资源类型 |
| 现有两个功能 | Fault 计数器、传感器高亮配色；**两个独立引擎，可只装一个、任意顺序叠加** |

**一句话方法论**：只要「改动落在托管 C# 逻辑里」且「后端是 Mono」，单凭一个运行包就足以开发，不需要源码工程。

---

## 1. 项目地图

### 1.1 两个游戏相关目录

| 目录 | 是什么 | 在开发中扮演的角色 |
| --- | --- | --- |
| `MajdataPlay_Build-LATEST/` | 已编译 Windows 运行包（约 5.9 GB），Mono 后端，含用户的 `MaiCharts/`、`Skins/`、`MajScores.db`、`settings.json`、`Logs/` | **真正的目标**。所有补丁只写它的 `Managed\Assembly-CSharp.dll` |
| `MajdataPlay-dev/` | 官方 dev 分支源码 zip（Unity 6000.3.17f1，bundleVersion 2.1.0，约 949 MB） | **仅作参考**。无 `.git`、无 `Library/`、5 个 submodule 全空；本机 Unity 版本不匹配，打不开也编译不了。其结构（语言包 `Translations` 字典、设置枚举）与 2.0.3 运行包**不同** |

> 曾经按 2.1.0 结构改过 10 个 dev 源码文件，保存在 `majdataplay-fault-counter` 仓库的 `dev-source-changes/`。**那是「未来完整 Build」路线的参考实现，绝不能用它编译出的 DLL 覆盖 2.0.3 运行包。**

### 1.2 三个公开仓库

| 仓库 | 内容 | 用途 |
| --- | --- | --- |
| [zYanzy/majdataplay-fault-counter](https://github.com/zYanzy/majdataplay-fault-counter) | 独立 Fault 补丁引擎、`apply_fault.py`、一键 bat、dev 源码改动、INSTALL.md | **多段注入的代码范本** |
| [zYanzy/majdataplay-sensor-color](https://github.com/zYanzy/majdataplay-sensor-color) | 独立配色引擎、tkinter GUI、INSTALL.md | **原地改常量 + 语义锚点的代码范本** |
| [zYanzy/majdataplay-custom](https://github.com/zYanzy/majdataplay-custom) | 归档导航页 + **本文件** + 拆分前的历史快照 | **项目入口 / 上下文来源** |

三个仓库均为公开，各含 GPLv3 `LICENSE`；都不含游戏二进制、谱面、成绩或个人数据。

### 1.3 关键本机资产（不在仓库里）

| 文件 | 作用 |
| --- | --- |
| 官方原版 `Assembly-CSharp.dll` | **验证基线**。SHA256 `139817f1e0147f8fe21fdc6c0a24303f3a93c98a7fd049c4eebf54a09c875ad9`，大小 `1,241,600` 字节 |
| `<GAME_DIR>\MajdataPlay_Data\app.info` | 版本标识文件 |
| 已补丁的 `Assembly-CSharp.dll` | 叠加验证用 |
| `sync_to_github.py` / 同步 bat | 仓库同步工具（本机网络限制见 §6.13） |
| `backups/`、`output/`、`config.json` | 运行时状态，**永不提交** |

> 指纹只对**未经修改**的官方原版 DLL 有效。DLL 一旦被任何补丁改过，就不要再比指纹，改看 `--inspect` 是否成功输出 JSON —— 那才是真正的锚点兼容性校验。

---

## 2. 为什么是 IL 补丁，而不是改源码重新 Build

这是本项目最重要的架构决策，理解它才不会走错路：

1. **完整 Build 不可行**：dev 工程要求 Unity `6000.3.17f1`，本机只有 `2022.3.57f1c1`；且 dev 目录来自 GitHub 分支 zip，没有 `.git`、`Library/`，5 个 submodule（HidSharp / ManagedBass / MajRadar / FFmpeg.AutoGen / UniTask）目录全空，直接打开会缺类型。要编译得先 `git clone --recurse-submodules -b dev https://github.com/LingFeng-bbben/MajdataPlay.git`。
2. **运行包是 Mono 后端**：存在 `MajdataPlay_Data\Managed\` 目录（219 个托管 DLL），C# 逻辑全部在 `Assembly-CSharp.dll` 里，可反编译、可改、可写回。若官方改用 **IL2CPP**，`Managed\Assembly-CSharp.dll` 会消失，整套方法直接失效。
3. **源码与运行包版本不同**：dev 是 2.1.0，运行包是 2.0.3，语言包格式、设置枚举、类结构都有差异。照 2.1.0 源码写出来的东西只能当参考，不能当成 2.0.3 的事实。
4. **结论**：反编译得到的 IL 才是本项目的「事实来源」。两个现有功能从头到尾都是只凭运行包做出来的，`grep` 全部脚本与引擎源码，对 dev 源码目录是**零引用**。

---

## 3. 环境准备

| 依赖 | 版本 / 说明 |
| --- | --- |
| Windows | 目标游戏为 Windows 包 |
| .NET SDK | **10**（`patcher.csproj` 目标框架 `net10.0`）；仅执行已编译的 `patcher.dll` 只需 .NET 10 **运行时** |
| Mono.Cecil | `0.11.6`，`patcher.csproj` 已引用，`dotnet build` 自动从 NuGet 还原 |
| ilspycmd | `dotnet tool install -g ilspycmd --version 11.2.0`，用于反编译定位 |
| Python 3 | 应用/回滚脚本只需标准库；若要 GUI 则**必须带 tkinter** |
| git | 可选，仅用于 clone / 版本管理 |

反编译常用命令（参数以 `ilspycmd --help` 为准）：

```bat
ilspycmd -o "<OUT_DIR>" "<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll"
ilspycmd -t MajdataPlay.ObjectCounter "<GAME_DIR>\MajdataPlay_Data\Managed\Assembly-CSharp.dll"
```

> Windows 上 `ilspycmd` 的全局工具 shim 有时不是真正的 exe。若调用失败，到 `%USERPROFILE%\.dotnet\tools\.store\` 下找到实际的 `ilspycmd.exe` 直接调用即可。
>
> **禁止**用 `dotnet build` / VS / MSBuild 编译游戏根目录的 `.sln` / `.csproj` —— 那些只给 IDE 提示用，且官方 README / AGENTS.md 明令禁止。

---

## 4. 代码资产地图：新功能该照哪个范本写

| 文件 | 什么时候读它 |
| --- | --- |
| `majdataplay-fault-counter/engine-src/Program.cs` | 需要**加枚举值、加字段、改方法体 IL、拦截方法开头、做幂等检测**时。它示范了 6 段注入与「已安装则跳过」 |
| `majdataplay-sensor-color/engine-src/Program.cs` | 需要**原地改常量、用语义锚点定位指令**时。它示范了 `brtrue` 跳转目标 → 4×`ldc.r4` → `Color::.ctor` 的完整定位与校验 |
| `*/engine-src/patcher.csproj` | 直接复制，不要改 `TargetFramework`；只替换 `Mono.Cecil` 之外的东西 |
| `majdataplay-fault-counter/apply_fault.py` | **安全应用脚本的标准范本**：`inspect 源 → 备份 → 生成 → staged inspect → 替换单文件 → final inspect`，任一步失败不写运行包，且检测游戏进程 |
| `*/restore.py` | 只回滚单个 DLL 的通用脚本 |
| `*/INSTALL.md` | 新功能做成可分发仓库时，照这个格式写部署指南（版本指纹、兼容性校验、备份、验收、回滚、禁止事项） |
| `majdataplay-sensor-color/sensor_color_tool.py` | 新功能若需要 GUI（预设色板、滑块、预览、一键应用、自动备份） |
| `dev-source-changes/` | 仅当想理解「若将来能完整 Build，源码级实现该怎么写」时参考 |

**两个引擎的 CLI 约定必须保持一致**（这是叠加能力的基础）：

```bat
dotnet "<TOOL_DIR>\engine\patcher.dll" "<INPUT.dll>" --inspect
dotnet "<TOOL_DIR>\engine\patcher.dll" "<INPUT.dll>" "<OUTPUT.dll>" [功能参数]
```

- `--inspect` 输出**一行 JSON**，且只含自己功能的状态字段。现有两份格式不同：Fault 是 `{"hasFault":true|false}`，配色是 `{"r":..,"g":..,"b":..,"a":..}`。旧合集版同时输出两组字段的格式**已废弃**，不要复用。
- `--inspect` 同时也是**版本兼容校验**：锚点找不到就抛异常、返回非 0。绝不能让它「容错继续」。

---

## 5. 新功能开发 SOP

### Step 1 — 先判断可行性，别急着写代码

依次回答三个问题，任一为否就停下来向用户说明，不要硬做：

1. **改动是否落在托管 C# 逻辑里？** 若是 prefab / 场景 / Shader / 贴图 / UI 层级 / `resources.assets` 里的语言包 / 新增 MonoBehaviour 或资源类型 → 这些是 Unity 序列化资源，`Mono.Cecil` 碰不到，需要可编译的 Unity 工程。
2. **后端是不是 Mono？**（存在 `MajdataPlay_Data\Managed\Assembly-CSharp.dll`）→ 若已变 IL2CPP，整套方法失效。
3. **版本是不是 2.0.3 `20260917-637bba0`？** → 不是就别复用任何现有锚点，必须全部重新定位；不要拿 2.1.0 源码或别的游戏版本 DLL 来套。

### Step 2 — 反编译定位

用 `ilspycmd` 导出目标类型，先读**语义**再读 IL。两个真实案例：

- Fault 不显示的根因，是靠反编译 `ObjectCounter` 初始化发现的：`_secondaryInfoDisplayOption = ...; if (_isJudgeTimingGaugeEnabled) _secondaryInfoDisplayOption = BGInfoOption.None;`
- 传感器高亮的锚点，是靠反编译 `DummyTouchPanelRenderer.OnPreUpdate()` 找到的：`flag ? new Color(0,0,0,0.3) : new Color(0,0,0,0)`。

把结果记下来：类型全名、方法名、关键 IL 指令序列、涉及的字段/枚举。

### Step 3 — 选语义锚点（最容易出错的一步）

- **不要依赖会被自己改掉的常量。** 早期配色补丁靠「找 `ldc.r4 0.3` 往前数 3 条」定位，用户把 alpha 改成别的值后锚点立刻失效。改为 **`brtrue.s` 的跳转目标**（并校验其后 3 条都是 `ldc.r4`、再后是 `Color::.ctor`）后才真正做到幂等。
- 优先级：分支跳转目标 > 特定方法引用（`TMP_Text::set_text` 等）> 指令序列形状 > 具体数值常量。
- 锚点定位失败必须让 `--inspect` 明确报错退出 —— 这正是防止把补丁打进不兼容版本的安全网。

### Step 4 — 写独立引擎（一个功能 = 一个引擎）

- 复制现有 `Program.cs` 骨架，保留 CLI 约定。
- **`--inspect` 只校验自己功能的锚点，输出只含自己状态的字段。**
- **绝不读写其他功能涉及的类型**：Fault 引擎只碰 `BGInfoOption` / `ObjectCounter`，配色引擎只碰 `DummyTouchPanelRenderer`。这是「可只装一个、任意顺序叠加」的唯一保证。
- **必须幂等**：输入可以是官方原版 DLL、已打过本补丁的 DLL、或已装了其他补丁的 DLL，都要能得到正确结果。
- **原地改 `Operand`，不要用 `ILProcessor.Replace`**：若某条指令同时是分支跳转目标（例如 4 个 `ldc.r4` 里的 R），重建 Instruction 对象会断开分支引用，生成损坏的程序集。
- 新功能若涉及 `settings.json` 里的新枚举值，注意 `BGInfoOption` 原为 `0..15`（`None=15`），Fault 已占用 `16`；新值不要改动已有数值，`settings.json` 用 Newtonsoft `StringEnumConverter` 按名字解析。

### Step 5 — 编译

```bat
dotnet build "<TOOL_DIR>\engine-src\patcher.csproj" -c Release -o "<TOOL_DIR>\engine"
```

### Step 6 — 交叉验证（在临时目录做，**不要碰运行包**）

以**官方原版 DLL** 为基线，至少跑完这四组：

| 场景 | 期望 |
| --- | --- |
| 只装新功能 A | A 生效；其他功能的状态字段保持不变（如颜色仍是 `(0,0,0,0.3)`） |
| 只装已有功能 B | B 生效；A 的状态显示未安装 |
| A → B 叠加 | A、B 同时生效 |
| B → A 叠加（反向） | A、B 同时生效 |
| 对「已打全部补丁的运行包 DLL」重打 A | A 幂等更新，B 依然保留 |

每组都用两个引擎各自的 `--inspect` 检查；再用 `ilspycmd` 反编译确认 IL 逻辑正确。这一步能挡住绝大多数「互相覆盖」类 bug。

### Step 7 — 写入运行包（严格照 `apply_fault.py` 的六步）

```
inspect 源 → 确认 MajdataPlay.exe 已退出并备份当前 DLL
→ 生成到 output/ → staged inspect 校验 → 只替换 Assembly-CSharp.dll → final inspect 校验
```

任何一步失败都**不要**替换运行包；生成物校验通过前绝不写入。

### Step 8 — 设置与验收

- `settings.json` 只改新功能必需的键，改前先备份，其余键一律保留。已知键位：`Game.BGInfo` / `Game.SecondaryBGInfo` / `Game.SubScreenBGInfo` / `Game.EnableJudgeTimingGauge` / `Debug.DisplaySensor`。
- **由用户手动双击 `MajdataPlay.exe`**，不要让 Agent 的命令会话启动游戏：那样启动的进程常被系统回收，且容易双开抢串口/音频。
- 检查游戏日志无 `InvalidProgramException` / `BadImageFormatException` / 程序集加载失败；`MaiCharts/`、`Skins/`、`MajScores.db` 未被改动。
- 若功能带 GUI，`config.json` 建议 `"auto_start_game": false`。

### Step 9 — 回滚

用备份覆盖回**单个 DLL** 即可。注意：**用官方原版 DLL 整体回滚会同时清掉所有已安装的 IL 补丁**，之后每个功能都要重新应用一次。

### Step 10 — 分发（可选）

一个功能 = 一个独立仓库，至少包含：`README.md`、`INSTALL.md`、`LICENSE`（GPLv3）、`.gitignore`、`.gitattributes`、`engine-src/`、`apply_*.py` / 一键 bat、`restore.py`。

- 仓库**不含** `engine/` 编译产物（`dotnet build` 可还原）；离线分发时才把整个目录连 `engine/` 一起打包。
- 提交前扫描个人路径、账号、邮箱；不得提交任何游戏二进制、谱面、成绩库、`settings.json`、`config.json`、`backups/`、`output/`。
- 给别的电脑部署时，写一份 `INSTALL.md` + 一段**指向它**的提示词（可参照 `majdataplay-custom` 归档仓库里的部署提示词范本）。

---

## 6. 铁律与已知坑

1. **MajdataHub 官方增量更新会覆盖 `Assembly-CSharp.dll`，所有补丁丢失。** 现在两个功能必须**分别**恢复：先跑 `apply_fault.py`，再用配色工具/配色引擎改色。配色引擎**不会**自动注入 Fault。
2. **`EnableJudgeTimingGauge` 陷阱**：信息项放在 `Game.SecondaryBGInfo` 时，`Game.EnableJudgeTimingGauge` 必须为 `false`。为 `true` 时初始化会把 `_secondaryInfoDisplayOption` 强制改成 `BGInfoOption.None`，`None` 分支隐藏整个副显示位 → 设置里选中了也不显示。主显示位 `BGInfo` 与副屏不受此限。
3. **语言包改不动**：`Localization.Init()` 用 `Resources.Load<TextAsset>("Langs/<code>")` 读打包进 `resources.assets` 的语言包，`StreamingAssets/Langs/*.json` 运行时无效。且运行包 2.0.3 是 `MappingTable` 数组 + `MAJSETTING_PROPERTY_<属性>_OPTION_<枚举名>` 格式，dev 2.1.0 是 `Translations` 字典 + `MAJSETTING_GENERAL_OPTION_*`。因此设置项中文名基本做不了（除非改 `resources.assets` 或 IL 注入词典）；HUD 标题若硬编码则不受影响，可显示为英文。
4. **4 个 `ldc.r4` 必须原地改 `Operand`**：R 指令同时是 `brtrue.s` 的跳转目标，用 `ILProcessor.Replace` 会断链。
5. **只替换程序文件，绝不整目录覆盖**，保留 `MaiCharts/`、`Skins/`、`*.db`、`settings.json`。
6. **禁止用 `dotnet build` / VS / MSBuild 编译游戏根目录 `.sln` / `.csproj`。**
7. **启动方式**：用户手动双击 `MajdataPlay.exe` 最可靠；桌面 `Majdata Hub.lnk` 是更新器不是游戏本体。
8. **既有非致命日志，不要误判成补丁导致的崩溃**：Burst `CompileILPPMethod2`、VisualScripting `PlayerPrefs`、`Scene transition overlay is not configured`、COM3/COM21 触控台、Discord 未运行、majdata.net SSL 失败、大量 `[i18n] Missing translation`。
9. **两份 `--inspect` 的 JSON 格式不同**，见 §4。写新引擎时不要复用旧合集版格式。
10. **官方原版 SHA256 指纹只对未改过的 DLL 有效**；改过就以 `--inspect` 结果为准。
11. **GUI 必须用 bat 启动**：WorkBuddy 内置便携 Python（PATH 首位）没有 tkinter；bat 会探测带 tkinter 的解释器并以 `pyw` 启动。升级代码后**旧工具窗口必须关掉重开**，否则内存里是旧代码，解析新 JSON 会 `KeyError`。
12. **公开仓库脱敏 + 许可**：官方源码 GPLv3，公开仓库须保留 `LICENSE`；对官方源码的修改同样按 GPLv3 提供。第三方 `Mono.Cecil` 为 MIT。不得包含个人路径、账号、邮箱。
13. **本机网络限制**：本机 `github.com` 主站被本地代理拒绝（502），`git push` **不可用**；`api.github.com` 可用。同步仓库请用 `sync_to_github.py`（走 REST API 写 Git 对象，自动跳过无变更、支持 `--repo NAME`），不要直接 push。对方的机器未必有此限制。
14. **dev 源码不是必需品**：「没有 `MajdataPlay-dev` 就没法开发」是误解。两个现有功能都只凭运行包完成。

---

## 7. 能力边界速查

| 需求类型 | 能否用 IL 补丁 | 替代方案 |
| --- | --- | --- |
| 新增 HUD 计数/显示项、改判定逻辑、改条件分支 | ✅ | — |
| 改硬编码颜色/数值常量 | ✅ | — |
| 新增枚举字段、类字段、方法 | ✅ | — |
| 设置菜单里选项的**中文名** | ❌ | 需改 `resources.assets` 或 IL 注入词典 |
| 改 prefab / 场景 / UI 层级 / 贴图 / Shader | ❌ | 需可编译 Unity 工程 + 完整 Build |
| 新增 MonoBehaviour / 新资源类型 | ❌ | 需编辑器生成序列化元数据 |
| 官方改用 IL2CPP 后端 | ❌ 整套方法失效 | 必须源码重编译 |

---

## 8. 交付前自检清单

- [ ] 新引擎 `--inspect` 只输出自己状态的 JSON，锚点找不到时报错退出
- [ ] 引擎幂等：原版 / 已打本补丁 / 已装其他补丁 三种输入都测过
- [ ] 六个交叉验证场景全部通过（§5 Step 6）
- [ ] 应用流程含备份 + 二次校验，且只替换单个 DLL
- [ ] `settings.json` 只改必要键，改前已备份
- [ ] 用户手动启动游戏后日志无程序集加载异常，个人数据未动
- [ ] 若新建仓库：README / INSTALL.md / LICENSE / .gitignore 齐全，已脱敏，无二进制
- [ ] 已向用户报告：改了哪些类型/方法、锚点选择依据、`--inspect` 输出、备份位置、需要在游戏里确认什么

---

## 9. 许可与声明

本项目是对 GPLv3 项目 [MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay) 的非官方修改，与其官方无关联。以 IL 补丁修改游戏程序集可能不符合相关使用条款，请自行评估；使用前务必备份，作者不对数据丢失或账号问题负责。
