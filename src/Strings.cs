// Strings: Chinese/English UI text, adapted from client.js DICT.
// Language follows the system UI culture (zh -> Chinese, otherwise English).
// C# 5 compatible (in-box csc.exe).
using System.Collections.Generic;
using System.Globalization;

internal static class Strings
{
    private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
    {
        { "AppTitle", "蓝牙耳机语音键" },
        { "AppName", "蓝牙耳机语音键" },
        { "Intro", "轻触左耳启动 Windows 语音输入，轻触右耳发送回删。对任何获得焦点的程序都有效，包括浏览器、聊天窗口和 AI 工具。" },
        { "Listening", "监听中" },
        { "Stopped", "未运行" },
        { "HelperStopped", "后台按键助手没有运行。" },
        { "Restart", "重新启动助手" },
        { "LeftTitle", "左耳" },
        { "LeftAction", "启动 Windows 语音输入（Win+H）" },
        { "RightTitle", "右耳" },
        { "RightAction", "发送回删（Backspace）" },
        { "KeyLabel", "识别到的按键" },
        { "ManualKey", "手动选择按键" },
        { "Learn", "学习按键" },
        { "LearningLeft", "现在请轻触左耳…" },
        { "LearningRight", "现在请轻触右耳…" },
        { "Test", "测试" },
        { "Testing", "已发送测试" },
        { "NotLearned", "尚未校准" },
        { "Backspaces", "每次回删字符数" },
        { "BackspaceHelp", "每轻触一次右耳，连续发送多少次 Backspace。" },
        { "Options", "行为设置" },
        { "Enabled", "启用快捷键" },
        { "EnabledHelp", "关闭后媒体键恢复正常，不需要卸载。" },
        { "Swallow", "拦截原始媒体键" },
        { "SwallowHelp", "建议开启，避免轻触耳机时同时暂停音乐或切换歌曲。" },
        { "AutoStart", "开机自启动" },
        { "AutoStartHelp", "Windows 登录后自动在后台运行。" },
        { "LastEvent", "最近一次耳机按键" },
        { "NoEvent", "还没有收到耳机按键。" },
        { "Recent", "最近的按键记录" },
        { "Conflict", "左右耳发送了同一个按键。Windows 无法凭这个手势区分左右耳。请在耳机 App 中给一侧换一个手势（例如上一曲/下一曲），然后重新学习左右耳。" },
        { "LearnConflict", "这个按键已经被另一侧使用。请给这一侧换一个手势，然后重新学习。" },
        { "LearnTimeout", "没有收到耳机按键。请再轻触一次，或确认耳机已连接并且会向 Windows 发送媒体键。" },
        { "VolumeModeTitle", "音量键手势模式" },
        { "VolumeMode", "你的耳机用音量变化传递触摸：左侧三击=音量减，右侧三击=音量加。程序识别手势后会还原音量，Windows 音量条可能短暂出现。" },
        { "LongPress", "长按的“嘟”声是耳机内部提示，不会发送给 Windows，因此不能作为快捷键触发条件。" },
        { "WarningTitle", "重要：Windows 看到的是媒体键/音量键，不是左耳或右耳" },
        { "Warning", "大多数蓝牙耳机左右耳会发送相同的命令，Windows 无法知道是哪一侧。只有两侧发送不同按键时，程序才能区分。你的耳机左侧三击发送“音量减”，右侧三击发送“音量加”，所以请用三击。长按的“嘟”声只存在于耳机内部，不会传给 Windows。" },
        { "SetupTitle", "首次使用前" },
        { "Setup", "1. 连接耳机，并把 Windows 设为音频输出。2. 在任意输入框按一次 Win+H；如果只弹出语音条但没有文字，请打开 Windows 设置 → 隐私和安全性 → 语音，开启“在线语音识别”。3. 在卡片里手动选择左右耳按键，或点“学习按键”。4. 如果你的耳机用音量键传递触摸，请用三击：左侧=音量减，右侧=音量加。" },
        { "Privacy", "按键在本地处理。本程序不会录音或上传音频；语音识别由 Windows 完成。" },
        { "VoiceTestHint", "2.5 秒后会发送 Win+H。现在请点击要语音输入的文本框。" },
        { "BackspaceTestHint", "已发送 Backspace。点击文本框后可以再试。" },
        { "StateFile", "已保存的映射" },
        { "OpenSettings", "打开设置" },
        { "EnableShortcuts", "启用快捷键" },
        { "Exit", "退出" },
        { "TrayRunning", "蓝牙耳机语音键 - 监听中" },
        { "TrayStopped", "蓝牙耳机语音键 - 未运行" },
        { "HideToTray", "已最小化到托盘，双击托盘图标可以重新打开设置。" },
        { "HookOk", "键盘钩子已安装" },
        { "HookFail", "键盘钩子未安装" },
        { "VolumeLevel", "当前音量" },
        { "Key_MediaPlayPause", "播放 / 暂停键" },
        { "Key_MediaNextTrack", "下一曲键" },
        { "Key_MediaPrevTrack", "上一曲键" },
        { "Key_MediaStop", "停止键" },
        { "Key_MediaSelect", "媒体选择键" },
        { "Key_VolumeMute", "静音键" },
        { "Key_VolumeDown", "音量减键" },
        { "Key_VolumeUp", "音量加键" },
        { "Key_BrowserBack", "浏览器后退键" },
        { "Key_BrowserForward", "浏览器前进键" },
        { "Key_Enter", "回车键" },
        { "Key_Space", "空格键" },
        { "Key_Tab", "Tab 键" },
        { "Key_Escape", "Esc 键" },
        { "Key_Backspace", "退格键" },
        { "Key_Delete", "Delete 键" },
    };

    private static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        { "AppTitle", "Bluetooth earbud keys" },
        { "AppName", "Bluetooth earbud keys" },
        { "Intro", "Tap the left earbud to start Windows voice typing. Tap the right earbud to send Backspace. Works in any focused app, including browsers, chat windows, and AI tools." },
        { "Listening", "Listening" },
        { "Stopped", "Not running" },
        { "HelperStopped", "The background helper is not running." },
        { "Restart", "Restart helper" },
        { "LeftTitle", "Left earbud" },
        { "LeftAction", "Start Windows voice typing (Win+H)" },
        { "RightTitle", "Right earbud" },
        { "RightAction", "Send Backspace" },
        { "KeyLabel", "Detected key" },
        { "ManualKey", "Choose key manually" },
        { "Learn", "Learn key" },
        { "LearningLeft", "Touch the left earbud now…" },
        { "LearningRight", "Touch the right earbud now…" },
        { "Test", "Test" },
        { "Testing", "Test sent" },
        { "NotLearned", "Not calibrated" },
        { "Backspaces", "Backspaces per tap" },
        { "BackspaceHelp", "Hold-free taps repeat the key; choose how many characters one tap removes." },
        { "Options", "Behavior" },
        { "Enabled", "Enable shortcuts" },
        { "EnabledHelp", "Turn off to let media keys behave normally without uninstalling." },
        { "Swallow", "Block the original media key" },
        { "SwallowHelp", "Recommended. Stops the earbud tap from also pausing music or changing tracks." },
        { "AutoStart", "Start with Windows" },
        { "AutoStartHelp", "Run in the background after Windows sign-in." },
        { "LastEvent", "Last earbud event" },
        { "NoEvent", "No earbud key received yet." },
        { "Recent", "Recent key events" },
        { "Conflict", "Both earbuds sent the same key. Windows cannot tell left from right for that gesture. In the earbud app, give one side a different gesture (for example previous/next track), then learn both sides again." },
        { "LearnConflict", "That is the key already used by the other earbud. Choose a different gesture for this side, then learn again." },
        { "LearnTimeout", "No earbud key arrived. Touch the earbud again, or check that it is connected and sends media keys to Windows." },
        { "VolumeModeTitle", "Volume-key gestures" },
        { "VolumeMode", "Your earbuds report touches as volume changes. Left triple-tap lowers the volume, right triple-tap raises it. The helper recognizes the gesture, restores the volume, and may briefly show the Windows volume bar." },
        { "LongPress", "A long-press beep is internal to the earbuds and is not sent to Windows, so it cannot be used as a shortcut trigger." },
        { "WarningTitle", "Important: Windows sees media/volume keys, not left/right earbuds" },
        { "Warning", "Most Bluetooth earbuds send the same command from both buds, and Windows cannot tell which ear touched. This app can separate them only when the two sides send different keys. Your earbuds send volume down from the left triple-tap and volume up from the right triple-tap, so use triple-tap. Long-press beeps stay inside the earbuds and never reach Windows." },
        { "SetupTitle", "Before first use" },
        { "Setup", "1. Connect the earbuds and set Windows as the audio output. 2. Press Win+H once in a text field. If the voice bar appears but no text is written, open Windows Settings → Privacy & security → Speech and turn on Online speech recognition. 3. Choose each earbud key manually or use Learn key. 4. If your earbuds send volume keys, use triple-tap: left = volume down, right = volume up." },
        { "Privacy", "Keys are handled locally. The app does not record or upload audio; Windows performs speech recognition." },
        { "VoiceTestHint", "Win+H will be sent in 2.5 seconds. Click into the text field where you want to dictate now." },
        { "BackspaceTestHint", "Backspace sent. Click into a text field to try it again." },
        { "StateFile", "Saved mapping" },
        { "OpenSettings", "Open settings" },
        { "EnableShortcuts", "Enable shortcuts" },
        { "Exit", "Exit" },
        { "TrayRunning", "Bluetooth earbud keys - listening" },
        { "TrayStopped", "Bluetooth earbud keys - not running" },
        { "HideToTray", "Minimized to the tray. Double-click the tray icon to open settings again." },
        { "HookOk", "Keyboard hook installed" },
        { "HookFail", "Keyboard hook not installed" },
        { "VolumeLevel", "Volume" },
        { "Key_MediaPlayPause", "Play / pause" },
        { "Key_MediaNextTrack", "Next track" },
        { "Key_MediaPrevTrack", "Previous track" },
        { "Key_MediaStop", "Stop" },
        { "Key_MediaSelect", "Media select" },
        { "Key_VolumeMute", "Mute" },
        { "Key_VolumeDown", "Volume down" },
        { "Key_VolumeUp", "Volume up" },
        { "Key_BrowserBack", "Browser back" },
        { "Key_BrowserForward", "Browser forward" },
        { "Key_Enter", "Enter" },
        { "Key_Space", "Space" },
        { "Key_Tab", "Tab" },
        { "Key_Escape", "Escape" },
        { "Key_Backspace", "Backspace" },
        { "Key_Delete", "Delete" },
    };

    public static readonly bool IsZh;

    static Strings()
    {
        string lang = "";
        try { lang = CultureInfo.CurrentUICulture.Name; } catch { lang = ""; }
        IsZh = lang.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);
    }

    public static string T(string key)
    {
        string value;
        if (IsZh)
        {
            if (Zh.TryGetValue(key, out value)) return value;
        }
        if (En.TryGetValue(key, out value)) return value;
        return key;
    }

    public static string KeyName(string key)
    {
        if (string.IsNullOrEmpty(key)) return T("NotLearned");
        if (key.StartsWith("vk:", System.StringComparison.OrdinalIgnoreCase)) return key;
        string label = T("Key_" + key);
        if (label != "Key_" + key) return label;
        return key;
    }
}
