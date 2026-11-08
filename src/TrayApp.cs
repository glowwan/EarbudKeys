// TrayApp: the tray icon, its menu, engine lifecycle, and the settings window.
// The engine's JSON lines are parsed here for status/recent-events display;
// learn completion is handled by SettingsForm (it owns the learn state).
// C# 5 compatible (in-box csc.exe).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

internal sealed class TrayApp : ApplicationContext
{
    private readonly Settings _settings;
    private readonly NotifyIcon _tray;
    private readonly MenuItem _enableItem;
    private readonly MenuItem _autoStartItem;
    private SettingsForm _form;
    private SynchronizationContext _uiCtx;
    private readonly List<string> _recent = new List<string>();
    private string _lastEventText = "";
    private string _lastError = "";
    private float _volumeLevel = -1f;
    private bool _exiting;
    private bool _hideTipShown;

    public Settings AppSettings { get { return _settings; } }
    public bool IsExiting { get { return _exiting; } }

    public TrayApp()
    {
        _settings = Settings.Load();
        _settings.AutoStart = Settings.ReadAutoStart();

        _tray = new NotifyIcon();
        _tray.Icon = Icons.AppIcon;
        _tray.Text = Strings.T("TrayStopped");
        _tray.Visible = true;
        _tray.DoubleClick += delegate { ShowSettings(); };

        var menu = new ContextMenu();
        var openItem = new MenuItem(Strings.T("OpenSettings"), delegate { ShowSettings(); });
        _enableItem = new MenuItem(Strings.T("EnableShortcuts"), delegate { ToggleEnabled(); });
        var restartItem = new MenuItem(Strings.T("Restart"), delegate { RestartEngine(); });
        _autoStartItem = new MenuItem(Strings.T("AutoStart"), delegate { ToggleAutoStart(); });
        var exitItem = new MenuItem(Strings.T("Exit"), delegate { ExitApp(); });
        menu.MenuItems.Add(openItem);
        menu.MenuItems.Add(_enableItem);
        menu.MenuItems.Add(restartItem);
        menu.MenuItems.Add(_autoStartItem);
        menu.MenuItems.Add(new MenuItem("-"));
        menu.MenuItems.Add(exitItem);
        _tray.ContextMenu = menu;

        EarbudEngine.Line += OnEngineLine;
        RefreshTrayMenu();
        ShowSettings();
        // The first Control creation installs the WinForms sync context;
        // capture it now so engine lines can marshal to the UI thread.
        _uiCtx = SynchronizationContext.Current;
        StartEngine();
    }

    public void ShowSettings()
    {
        if (_form == null || _form.IsDisposed)
        {
            _form = new SettingsForm(this);
        }
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.BringToFront();
        _form.Activate();
    }

    public void HideSettings()
    {
        if (_form != null && !_form.IsDisposed) _form.Hide();
        if (!_hideTipShown)
        {
            _hideTipShown = true;
            ShowBalloon(Strings.T("HideToTray"));
        }
    }

    public void ShowBalloon(string text)
    {
        try { _tray.ShowBalloonTip(3000, Strings.T("AppTitle"), text, ToolTipIcon.Info); }
        catch { }
    }

    public void RefreshTrayMenu()
    {
        if (_enableItem != null) _enableItem.Checked = _settings.Enabled;
        if (_autoStartItem != null) _autoStartItem.Checked = _settings.AutoStart;
    }

    public void RefreshForm()
    {
        if (_form != null && !_form.IsDisposed && _form.Visible)
        {
            _form.RefreshStatus();
        }
    }

    public string LastError { get { return _lastError; } }
    public float VolumeLevel { get { return _volumeLevel; } }
    public string LastEventText { get { return _lastEventText; } }

    public string[] RecentEvents()
    {
        lock (_recent)
        {
            return _recent.ToArray();
        }
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        SaveAndApply();
        RefreshTrayMenu();
        RefreshForm();
    }

    private void ToggleAutoStart()
    {
        bool next = !_settings.AutoStart;
        if (Settings.WriteAutoStart(next))
        {
            _settings.AutoStart = next;
        }
        else
        {
            _settings.AutoStart = Settings.ReadAutoStart();
        }
        _settings.Save();
        RefreshTrayMenu();
        RefreshForm();
    }

    /// <summary>Saves the settings file and pushes the mapping to the engine.</summary>
    public void SaveAndApply()
    {
        _settings.Save();
        if (EarbudEngine.Running)
        {
            EarbudEngine.ApplyConfigJson(_settings.ToJson());
        }
    }

    public void RestartEngine()
    {
        StopEngine();
        Thread.Sleep(150);
        StartEngine();
        RefreshForm();
    }

    private void StartEngine()
    {
        _lastError = "";
        bool ok = false;
        try
        {
            ok = EarbudEngine.Start();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            ok = false;
        }
        if (!ok)
        {
            if (string.IsNullOrEmpty(_lastError)) _lastError = EarbudEngine.LastError ?? "";
            _tray.Text = Strings.T("TrayStopped");
            ShowBalloon(Strings.T("HelperStopped") + " " + _lastError);
            return;
        }
        EarbudEngine.ApplyConfigJson(_settings.ToJson());
        _tray.Text = Strings.T("TrayRunning");
    }

    private void StopEngine()
    {
        try { EarbudEngine.Stop(); }
        catch { }
        _tray.Text = Strings.T("TrayStopped");
    }

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        EarbudEngine.Line -= OnEngineLine;
        StopEngine();
        try { _tray.Visible = false; } catch { }
        try { _tray.Dispose(); } catch { }
        Application.Exit();
    }

    private void OnEngineLine(string json)
    {
        string type = EarbudEngine.GetString(json, "type");
        if (type == "event")
        {
            string side = EarbudEngine.GetString(json, "side");
            string key = EarbudEngine.GetString(json, "key");
            string action = EarbudEngine.GetString(json, "action");
            string sideText = side == "right" ? Strings.T("RightTitle") : Strings.T("LeftTitle");
            string text = DateTime.Now.ToString("HH:mm:ss") + "  " + sideText + " · " + Strings.KeyName(key) + " → " + action;
            _lastEventText = text;
            lock (_recent)
            {
                _recent.Insert(0, text);
                while (_recent.Count > 14) _recent.RemoveAt(_recent.Count - 1);
            }
        }
        else if (type == "volume")
        {
            _volumeLevel = (float)EarbudEngine.GetDouble(json, "level", -1);
        }
        else if (type == "error")
        {
            _lastError = EarbudEngine.GetString(json, "message");
        }
        else if (type == "ready")
        {
            _lastError = "";
        }
        else if (type == "stopped")
        {
            _tray.Text = Strings.T("TrayStopped");
        }
        PostToUi(delegate { RefreshForm(); });
    }

    private void PostToUi(Action action)
    {
        if (_uiCtx == null) _uiCtx = SynchronizationContext.Current;
        if (_uiCtx != null)
        {
            _uiCtx.Post(delegate { action(); }, null);
            return;
        }
        if (_form != null && !_form.IsDisposed && _form.IsHandleCreated)
        {
            try { _form.BeginInvoke(action); } catch { }
            return;
        }
        action();
    }
}
