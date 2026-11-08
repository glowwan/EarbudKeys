// Settings: load/save the earbud mapping and app options.
// Same fields and defaults as the DSH plugin (index.js DEFAULT_CONFIG),
// plus AutoStart. Stored at %APPDATA%\EarbudKeys\settings.json.
// C# 5 compatible (in-box csc.exe).
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

internal sealed class Settings
{
    public bool Enabled = true;
    public string LeftKey = "VolumeDown";
    public string RightKey = "VolumeUp";
    public string LeftAction = "voice";
    public string RightAction = "backspace";
    public int BackspaceCount = 1;
    public bool Swallow = true;
    public bool AutoStart = false;

    private static readonly HashSet<string> KeyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "MediaPlayPause", "MediaNextTrack", "MediaPrevTrack", "MediaStop", "MediaSelect",
        "VolumeMute", "VolumeDown", "VolumeUp", "BrowserBack", "BrowserForward",
        "Enter", "Space", "Tab", "Escape", "Backspace", "Delete",
    };
    private static readonly HashSet<string> Actions = new HashSet<string>(StringComparer.Ordinal)
    {
        "voice", "backspace", "enter", "space", "tab", "escape", "delete", "none",
    };

    public static string SettingsPath
    {
        get
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EarbudKeys");
            return Path.Combine(dir, "settings.json");
        }
    }

    public static Settings Load()
    {
        var settings = new Settings();
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path)) return settings;
            string json = File.ReadAllText(path);
            string key = EarbudEngine.GetString(json, "leftKey");
            if (IsKey(key)) settings.LeftKey = key;
            key = EarbudEngine.GetString(json, "rightKey");
            if (IsKey(key)) settings.RightKey = key;
            string action = EarbudEngine.GetString(json, "leftAction");
            if (Actions.Contains(action)) settings.LeftAction = action;
            action = EarbudEngine.GetString(json, "rightAction");
            if (Actions.Contains(action)) settings.RightAction = action;
            int count = EarbudEngine.GetInt(json, "backspaceCount", settings.BackspaceCount);
            settings.BackspaceCount = Math.Max(1, Math.Min(20, count));
            string raw = EarbudEngine.GetRaw(json, "enabled");
            if (raw == "true" || raw == "false") settings.Enabled = raw == "true";
            raw = EarbudEngine.GetRaw(json, "swallow");
            if (raw == "true" || raw == "false") settings.Swallow = raw == "true";
            raw = EarbudEngine.GetRaw(json, "autoStart");
            if (raw == "true" || raw == "false") settings.AutoStart = raw == "true";
        }
        catch
        {
            // Corrupt settings: fall back to defaults.
        }
        return settings;
    }

    public void Save()
    {
        try
        {
            string path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + ".tmp";
            File.WriteAllText(temp, ToJson(), System.Text.Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        catch
        {
            // Saving is best effort; the live mapping already applies.
        }
    }

    public string ToJson()
    {
        return "{\n"
            + "  \"enabled\": " + (Enabled ? "true" : "false") + ",\n"
            + "  \"leftKey\": " + EarbudEngine.Json(LeftKey) + ",\n"
            + "  \"rightKey\": " + EarbudEngine.Json(RightKey) + ",\n"
            + "  \"leftAction\": " + EarbudEngine.Json(LeftAction) + ",\n"
            + "  \"rightAction\": " + EarbudEngine.Json(RightAction) + ",\n"
            + "  \"backspaceCount\": " + BackspaceCount.ToString() + ",\n"
            + "  \"swallow\": " + (Swallow ? "true" : "false") + ",\n"
            + "  \"autoStart\": " + (AutoStart ? "true" : "false") + "\n"
            + "}";
    }

    public bool HasConflict()
    {
        return string.Equals(LeftKey, RightKey, StringComparison.Ordinal);
    }

    private static bool IsKey(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (KeyNames.Contains(value)) return true;
        if (value.Length > 3 && value.Substring(0, 3).Equals("vk:", StringComparison.OrdinalIgnoreCase))
        {
            string raw = value.Substring(3);
            int parsed;
            if (raw.Length > 2 && raw.Substring(0, 2).Equals("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(raw.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out parsed);
            }
            return int.TryParse(raw, out parsed);
        }
        return false;
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "EarbudKeys";

    /// <summary>Reads the HKCU Run key (the real autostart state).</summary>
    public static bool ReadAutoStart()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null) return false;
                object value = key.GetValue(RunValueName);
                return value is string;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Writes or removes the HKCU Run key.</summary>
    public static bool WriteAutoStart(bool enable)
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return false;
            using (key)
            {
                if (enable)
                {
                    string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(RunValueName, "\"" + exe + "\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
