# 蓝牙耳机语音键（独立版）EarbudKeys

把蓝牙耳机可被 Windows 看到的按键变成桌面级**语音输入**和**回删**快捷键，用于浏览器、聊天窗口、AI 客户端和 AI Agent 程序，替代手工键盘输入。不需要 DeepSeek Harness，不需要安装任何运行环境，双击安装就能用。

- **左侧耳机三击**（发送音量减）→ 发送 `Win+H`，启动 Windows 语音输入，把话输入到当前获得焦点的输入框。
- **右侧耳机三击**（发送音量加）→ 发送 `Backspace`，删除光标前的字符。

改编自开源项目 [glowwan/dsh-bluetooth-earbuds](https://github.com/glowwan/dsh-bluetooth-earbuds)（MIT 协议），原项目是 DeepSeek Harness 的插件，只能在 Harness 环境下运作。本独立版把它的核心逻辑原样保留，去掉了对 Harness 的依赖。

## 安装

运行 `Setup-EarbudKeys-1.0.0.exe`，按提示安装。安装位置默认为 `C:\Program Files\EarbudKeys`，开始菜单会有快捷方式；安装时可以勾选“桌面快捷方式”和“开机自启动”。

要求：Windows 10 / 11，不需要安装 .NET SDK（.NET Framework 4 随 Windows 自带）。

卸载：Windows 设置 → 应用 → 蓝牙耳机语音键 → 卸载。卸载时会问要不要同时删除已保存的耳机映射。

## 首次使用

1. 安装后程序自动打开设置窗口，状态显示“监听中”。
2. 让 `Win+H` 可以转写：在任意输入框按一次 `Win+H`，对麦克风说一句话。如果只弹出语音条但没有文字，打开 **Windows 设置 → 隐私和安全性 → 语音**，开启**在线语音识别**。
3. 在左右耳卡片中确认按键：左耳 `音量减键`、右耳 `音量加键`；也可以点“学习按键”，然后按对应手势（学习需要真的触发一次音量/媒体键）。
4. 点**测试**。左耳测试会在 2.5 秒后发送 `Win+H`，请先点进要输入文字的文本框；右耳测试会发送一次 Backspace。

映射保存在：

```
%APPDATA%\EarbudKeys\settings.json
```

## 行为选项

- **启用快捷键**：关闭后音量/媒体键恢复正常，不必卸载。
- **拦截原始媒体键**：对普通媒体键生效；音量键手势由音量变化检测触发，系统音量条仍可能短暂出现。
- **每次回删字符数**：右耳每触发一次发送的 Backspace 次数，可选 1–10。
- **开机自启动**：Windows 登录后自动在后台运行。

关闭设置窗口后程序继续在托盘运行，双击托盘图标可以重新打开设置；右键托盘图标可以开关快捷键、重新启动助手、退出。

## 重要限制：Windows 只能看到按键，不知道是左耳还是右耳

蓝牙耳机通过 AVRCP/HID 向电脑发送“播放/暂停”“音量加/减”等命令，**不会告诉 Windows 是左耳还是右耳在触摸**。因此：

- 只有两侧发送**不同的键**时，程序才能区分左右耳。
- 如果两侧发送同一个键（例如都发送播放/暂停），任何软件都无法区分。
- 部分耳机把触摸用于唤醒手机助手，或只通过蓝牙 HFP 发送私有事件；这类操作不会进入 Windows，程序也收不到。

设置页会在两侧选到同一个键时拒绝保存并提示。

长按的“嘟”声只是耳机内部提示音，不会作为按键发送给 Windows，所以不能作为触发条件。

## 工作原理

程序启动后同时使用多条检测路径：

- 低级键盘钩子、Raw Input、`GetAsyncKeyState` 轮询，用于普通媒体键；
- Core Audio 音量变化检测，用于耳机只发送音量增减的情况。检测到一次手势后等音量稳定，再执行动作并把音量还原。

左耳触发 `SendInput` 注入 `Win+H`，右耳注入 `Backspace`。程序不录音、不联网、不上传音频。语音识别由 Windows 语音输入完成；开启“在线语音识别”后，语音会按 Microsoft 的隐私政策发送到 Microsoft。

## 与原插件的对应关系

| 原插件（DSH） | 独立版 |
| --- | --- |
| `helper/EarbudHelper.cs`（950 行，检测引擎） | `src/EarbudEngine.cs`：逻辑原样保留，改成进程内调用，不再走 stdin/stdout JSON 管道 |
| `index.js`（Host：助手生命周期、配置保存、HTTP 接口、Agent 工具） | `src/TrayApp.cs` + `src/Settings.cs`：托盘程序管理引擎生命周期，配置存 `%APPDATA%\EarbudKeys\settings.json`；Agent 工具没有对应物（独立版不接 Agent） |
| `client.js`（DSH 设置页，React） | `src/SettingsForm.cs`：原生 WinForms 设置窗口，功能对等（状态、学习、测试、回删数、开关、最近记录） |
| `cordis.patch.yml`、插件市场元数据 | 不需要，已删除 |
| 首次启动用 `csc.exe` 编译助手 | 安装包自带已编译好的 `EarbudKeys.exe`，不需要现场编译 |

默认映射与原插件一致：左耳 `VolumeDown` → 语音，右耳 `VolumeUp` → 回删，每次回删 1 个字符，拦截原始媒体键开启。

另修了一个原插件里的小问题：音量值用当前系统语言格式化，在小数点为逗号的语言环境下会产生无效 JSON；独立版改用不变文化（InvariantCulture）格式化。

## 排错

| 现象 | 处理 |
| --- | --- |
| 状态显示“未运行” | 点“重新启动助手”。若仍失败，查看状态栏显示的错误。 |
| 三击没有反应 | 确认左右耳分别选的是“音量减键”/“音量加键”，并且状态为“监听中”。音量已经在最大/最小时可能无法检测。 |
| `Win+H` 只弹窗、不出字 | 打开 **设置 → 隐私和安全性 → 语音 → 在线语音识别**；检查默认麦克风没有被静音。 |
| 左耳触发了语音，但没有文字 | 先在记事本手动按 `Win+H` 确认能转写，再试耳机。 |
| 右耳没有回删 | 先把光标点进一个有文字的文本框；Backspace 只删除光标前的内容。 |
| 长按没有反应 | 长按只产生耳机内部提示音，不发送按键，无法被软件检测。 |
| 音量条每次都会闪 | 这是 Windows 对音量键的系统提示；程序会还原音量，但无法完全隐藏系统音量条。 |
| 对某些程序无效 | Windows 不允许普通权限程序向管理员权限窗口注入按键；以相同权限运行目标程序，或不要以管理员身份运行目标程序。 |

## 从源码构建（Windows）

不需要安装任何东西，双击 `build.bat` 即可。它会用 Windows 自带的 `csc.exe`（.NET Framework 4）编译 `src\*.cs`，生成 `EarbudKeys.exe`。

也可以在 Linux 上用 Mono 编译验证（只做编译检查，不可运行）：

```sh
mcs -target:winexe -r:System.Windows.Forms -r:System.Drawing \
    -win32icon:app.ico -out:EarbudKeys.exe src/*.cs
```

安装包用 NSIS 构建：

```sh
makensis installer.nsi
```

## 文件

- `src/EarbudEngine.cs` — Windows 检测引擎（键盘钩子、Raw Input、轮询、音量手势、按键注入）。
- `src/TrayApp.cs` — 托盘程序：引擎生命周期、托盘菜单、状态与最近记录。
- `src/SettingsForm.cs` — 设置窗口。
- `src/Settings.cs` — 配置读写与开机自启动（注册表 Run 键）。
- `src/Strings.cs` — 中英文界面文字（跟随系统语言）。
- `src/Icons.cs` — 运行时绘制的托盘/窗口图标。
- `src/Program.cs` — 入口（单实例、消息循环）。
- `tools/make-icon.py` — 生成 `app.ico`（供 exe 嵌入与安装包快捷方式使用）。
- `build.bat` — Windows 下用自带 csc.exe 编译。
- `installer.nsi` — NSIS 安装包脚本。
- `LICENSE` — MIT 协议（与原项目相同）。

## 许可证

MIT，与原项目 [glowwan/dsh-bluetooth-earbuds](https://github.com/glowwan/dsh-bluetooth-earbuds) 相同。

## 验证记录（2026-11-08，Linux + Mono/Xvfb 实测）

- `dotnet build` 0 警告 0 错误；安装包内容经 7z 解包核对无误。
- 设置界面正常渲染；Test 按钮、左右耳下拉选择、Learn（60 秒超时）、复选框逐一实测可用；改动实时写入 `%APPDATA%\EarbudKeys\settings.json`，重启后正确读回。
- 配置层单测 24/24 通过（默认值、保存/读回、左右键冲突检测、注册表自启）。
- 健壮性：Win32 API 不可用时（如本测试环境）检测线程报错但不崩溃，进程保持运行；在真 Windows 上这些 API 均存在。
- 2026-11-08 真机实测：用户在 Windows 11 笔记本上用蓝牙耳机测试，左右耳三击、学习、测试全部正常，工作完美。首次使用仍请按界面内“首次使用前”说明操作。
