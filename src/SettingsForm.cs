// SettingsForm: the settings window. Mirrors the DSH settings page
// (client.js): status, left/right earbud cards with learn/test, behavior
// options, recent events, and notes. Closes to the tray; Exit lives in
// the tray menu. C# 5 compatible (in-box csc.exe).
using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class SettingsForm : Form
{
    private readonly TrayApp _app;
    private readonly Settings _settings;

    private Label _statusLabel;
    private Label _statusSubLabel;
    private Label _dotLabel;
    private Button _restartButton;
    private Label _errorLabel;

    private Label _leftKeyValue;
    private ComboBox _leftKeyBox;
    private Button _leftLearnButton;
    private Button _leftTestButton;
    private Label _leftHint;
    private Label _rightKeyValue;
    private ComboBox _rightKeyBox;
    private Button _rightLearnButton;
    private Button _rightTestButton;
    private Label _rightHint;

    private ComboBox _backspaceBox;
    private CheckBox _enabledBox;
    private CheckBox _swallowBox;
    private CheckBox _autoStartBox;
    private ListBox _recentBox;
    private TextBox _notesBox;

    private string _pendingLearn = "";
    private bool _loadingValues;
    private readonly Timer _learnTimer;
    private readonly Timer _statusTimer;

    private static readonly string[] SelectableKeys = new string[]
    {
        "VolumeDown", "VolumeUp", "MediaPlayPause", "MediaPrevTrack", "MediaNextTrack", "MediaStop",
    };

    private sealed class KeyItem
    {
        public string Key;
        public string Display;
        public override string ToString() { return Display; }
    }

    public SettingsForm(TrayApp app)
    {
        _app = app;
        _settings = app.AppSettings;
        this.Text = Strings.T("AppTitle");
        this.Icon = Icons.AppIcon;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(660, 600);
        this.Size = new Size(800, 800);

        var flow = new FlowLayoutPanel();
        flow.Dock = DockStyle.Fill;
        flow.FlowDirection = FlowDirection.TopDown;
        flow.WrapContents = false;
        flow.AutoScroll = true;
        flow.Padding = new Padding(12);
        this.Controls.Add(flow);

        var intro = new Label();
        intro.AutoSize = true;
        intro.MaximumSize = new Size(740, 0);
        intro.Text = Strings.T("Intro");
        flow.Controls.Add(intro);

        flow.Controls.Add(BuildStatusPanel());
        _errorLabel = new Label();
        _errorLabel.AutoSize = true;
        _errorLabel.MaximumSize = new Size(740, 0);
        _errorLabel.ForeColor = Color.DarkRed;
        _errorLabel.Visible = false;
        flow.Controls.Add(_errorLabel);

        var cards = new TableLayoutPanel();
        cards.Width = 740;
        cards.Height = 250;
        cards.ColumnCount = 2;
        cards.RowCount = 1;
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        cards.Controls.Add(BuildSideCard(true), 0, 0);
        cards.Controls.Add(BuildSideCard(false), 1, 0);
        flow.Controls.Add(cards);

        flow.Controls.Add(BuildOptionsBox());

        var recentBox = new GroupBox();
        recentBox.Text = Strings.T("Recent");
        recentBox.Width = 740;
        recentBox.Height = 140;
        _recentBox = new ListBox();
        _recentBox.Dock = DockStyle.Fill;
        _recentBox.Font = new Font("Consolas", 9f);
        recentBox.Controls.Add(_recentBox);
        flow.Controls.Add(recentBox);

        var notesBox = new GroupBox();
        notesBox.Text = Strings.T("SetupTitle");
        notesBox.Width = 740;
        notesBox.Height = 230;
        _notesBox = new TextBox();
        _notesBox.Dock = DockStyle.Fill;
        _notesBox.Multiline = true;
        _notesBox.ReadOnly = true;
        _notesBox.ScrollBars = ScrollBars.Vertical;
        _notesBox.Text = BuildNotesText();
        notesBox.Controls.Add(_notesBox);
        flow.Controls.Add(notesBox);

        var pathLabel = new Label();
        pathLabel.AutoSize = true;
        pathLabel.MaximumSize = new Size(740, 0);
        pathLabel.ForeColor = Color.Gray;
        pathLabel.Text = Strings.T("StateFile") + ": " + Settings.SettingsPath;
        flow.Controls.Add(pathLabel);

        _learnTimer = new Timer();
        _learnTimer.Interval = 60000;
        _learnTimer.Tick += delegate
        {
            _learnTimer.Stop();
            if (_pendingLearn.Length > 0)
            {
                string side = _pendingLearn;
                _pendingLearn = "";
                EarbudEngine.CancelLearn();
                SetHint(side, Strings.T("LearnTimeout"));
                RefreshLearnButtons();
            }
        };

        _statusTimer = new Timer();
        _statusTimer.Interval = 1500;
        _statusTimer.Tick += delegate { RefreshStatus(); };
        _statusTimer.Start();

        EarbudEngine.Line += OnEngineLine;
        LoadValuesFromSettings();
        RefreshStatus();
    }

    private Panel BuildStatusPanel()
    {
        var panel = new Panel();
        panel.Width = 740;
        panel.Height = 54;
        panel.BorderStyle = BorderStyle.FixedSingle;

        _dotLabel = new Label();
        _dotLabel.Text = "●";
        _dotLabel.Font = new Font(this.Font.FontFamily, 14f);
        _dotLabel.ForeColor = Color.Gray;
        _dotLabel.Location = new Point(12, 14);
        _dotLabel.AutoSize = true;
        panel.Controls.Add(_dotLabel);

        _statusLabel = new Label();
        _statusLabel.Font = new Font(this.Font, FontStyle.Bold);
        _statusLabel.Location = new Point(38, 8);
        _statusLabel.AutoSize = true;
        panel.Controls.Add(_statusLabel);

        _statusSubLabel = new Label();
        _statusSubLabel.ForeColor = Color.Gray;
        _statusSubLabel.Location = new Point(38, 28);
        _statusSubLabel.AutoSize = true;
        _statusSubLabel.MaximumSize = new Size(560, 0);
        panel.Controls.Add(_statusSubLabel);

        _restartButton = new Button();
        _restartButton.Text = Strings.T("Restart");
        _restartButton.Location = new Point(640, 12);
        _restartButton.Size = new Size(88, 28);
        _restartButton.Click += delegate { _app.RestartEngine(); };
        panel.Controls.Add(_restartButton);

        return panel;
    }

    private GroupBox BuildSideCard(bool left)
    {
        string title = left ? Strings.T("LeftTitle") : Strings.T("RightTitle");
        string action = left ? Strings.T("LeftAction") : Strings.T("RightAction");
        var box = new GroupBox();
        box.Text = title + " — " + action;
        box.Dock = DockStyle.Fill;

        var inner = new FlowLayoutPanel();
        inner.Dock = DockStyle.Fill;
        inner.FlowDirection = FlowDirection.TopDown;
        inner.WrapContents = false;
        inner.Padding = new Padding(8);
        box.Controls.Add(inner);

        var keyRow = new Panel();
        keyRow.Width = 330;
        keyRow.Height = 26;
        var keyLabel = new Label();
        keyLabel.Text = Strings.T("KeyLabel");
        keyLabel.ForeColor = Color.Gray;
        keyLabel.Location = new Point(0, 4);
        keyLabel.AutoSize = true;
        keyRow.Controls.Add(keyLabel);
        var keyValue = new Label();
        keyValue.Font = new Font(this.Font, FontStyle.Bold);
        keyValue.Location = new Point(120, 4);
        keyValue.AutoSize = true;
        keyRow.Controls.Add(keyValue);
        inner.Controls.Add(keyRow);
        if (left) _leftKeyValue = keyValue; else _rightKeyValue = keyValue;

        var manualRow = new Panel();
        manualRow.Width = 330;
        manualRow.Height = 30;
        var manualLabel = new Label();
        manualLabel.Text = Strings.T("ManualKey");
        manualLabel.ForeColor = Color.Gray;
        manualLabel.Location = new Point(0, 6);
        manualLabel.AutoSize = true;
        manualRow.Controls.Add(manualLabel);
        var keyBox = new ComboBox();
        keyBox.DropDownStyle = ComboBoxStyle.DropDownList;
        keyBox.Location = new Point(120, 3);
        keyBox.Width = 200;
        manualRow.Controls.Add(keyBox);
        inner.Controls.Add(manualRow);
        if (left) _leftKeyBox = keyBox; else _rightKeyBox = keyBox;

        var buttonRow = new Panel();
        buttonRow.Width = 330;
        buttonRow.Height = 34;
        var learnButton = new Button();
        learnButton.Text = Strings.T("Learn");
        learnButton.Location = new Point(0, 3);
        learnButton.Size = new Size(100, 28);
        buttonRow.Controls.Add(learnButton);
        var testButton = new Button();
        testButton.Text = Strings.T("Test");
        testButton.Location = new Point(110, 3);
        testButton.Size = new Size(80, 28);
        buttonRow.Controls.Add(testButton);
        inner.Controls.Add(buttonRow);
        if (left)
        {
            _leftLearnButton = learnButton;
            _leftTestButton = testButton;
            learnButton.Click += delegate { Learn("left"); };
            testButton.Click += delegate { Test("left"); };
        }
        else
        {
            _rightLearnButton = learnButton;
            _rightTestButton = testButton;
            learnButton.Click += delegate { Learn("right"); };
            testButton.Click += delegate { Test("right"); };
        }

        var hint = new Label();
        hint.AutoSize = true;
        hint.MaximumSize = new Size(330, 0);
        hint.ForeColor = Color.DarkOrange;
        inner.Controls.Add(hint);
        if (left) _leftHint = hint; else _rightHint = hint;

        keyBox.SelectedIndexChanged += delegate
        {
            if (_loadingValues) return;
            KeyItem item = keyBox.SelectedItem as KeyItem;
            if (item == null) return;
            if (left) _settings.LeftKey = item.Key; else _settings.RightKey = item.Key;
            SaveSettings();
        };

        return box;
    }

    private GroupBox BuildOptionsBox()
    {
        var box = new GroupBox();
        box.Text = Strings.T("Options");
        box.Width = 740;
        box.Height = 168;
        var inner = new FlowLayoutPanel();
        inner.Dock = DockStyle.Fill;
        inner.FlowDirection = FlowDirection.TopDown;
        inner.WrapContents = false;
        inner.Padding = new Padding(8);
        box.Controls.Add(inner);

        var backRow = new Panel();
        backRow.Width = 700;
        backRow.Height = 30;
        var backLabel = new Label();
        backLabel.Text = Strings.T("Backspaces");
        backLabel.Location = new Point(0, 6);
        backLabel.AutoSize = true;
        backRow.Controls.Add(backLabel);
        _backspaceBox = new ComboBox();
        _backspaceBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _backspaceBox.Location = new Point(150, 3);
        _backspaceBox.Width = 80;
        foreach (int value in new int[] { 1, 2, 3, 4, 5, 10 }) _backspaceBox.Items.Add(value.ToString());
        _backspaceBox.SelectedIndexChanged += delegate
        {
            if (_loadingValues) return;
            int value;
            if (int.TryParse(_backspaceBox.SelectedItem as string, out value))
            {
                _settings.BackspaceCount = value;
                SaveSettings();
            }
        };
        backRow.Controls.Add(_backspaceBox);
        var backHelp = new Label();
        backHelp.Text = Strings.T("BackspaceHelp");
        backHelp.ForeColor = Color.Gray;
        backHelp.Location = new Point(240, 6);
        backHelp.AutoSize = true;
        backRow.Controls.Add(backHelp);
        inner.Controls.Add(backRow);

        _enabledBox = new CheckBox();
        _enabledBox.Text = Strings.T("Enabled") + "  —  " + Strings.T("EnabledHelp");
        _enabledBox.AutoSize = true;
        _enabledBox.CheckedChanged += delegate
        {
            if (_loadingValues) return;
            _settings.Enabled = _enabledBox.Checked;
            SaveSettings();
            _app.RefreshTrayMenu();
        };
        inner.Controls.Add(_enabledBox);

        _swallowBox = new CheckBox();
        _swallowBox.Text = Strings.T("Swallow") + "  —  " + Strings.T("SwallowHelp");
        _swallowBox.AutoSize = true;
        _swallowBox.CheckedChanged += delegate
        {
            if (_loadingValues) return;
            _settings.Swallow = _swallowBox.Checked;
            SaveSettings();
        };
        inner.Controls.Add(_swallowBox);

        _autoStartBox = new CheckBox();
        _autoStartBox.Text = Strings.T("AutoStart") + "  —  " + Strings.T("AutoStartHelp");
        _autoStartBox.AutoSize = true;
        _autoStartBox.CheckedChanged += delegate
        {
            if (_loadingValues) return;
            if (Settings.WriteAutoStart(_autoStartBox.Checked))
            {
                _settings.AutoStart = _autoStartBox.Checked;
            }
            else
            {
                _loadingValues = true;
                _settings.AutoStart = Settings.ReadAutoStart();
                _autoStartBox.Checked = _settings.AutoStart;
                _loadingValues = false;
            }
            _settings.Save();
            _app.RefreshTrayMenu();
        };
        inner.Controls.Add(_autoStartBox);

        return box;
    }

    private string BuildNotesText()
    {
        string text = "";
        bool volumeMode = _settings.LeftKey == "VolumeDown" || _settings.LeftKey == "VolumeUp"
            || _settings.RightKey == "VolumeDown" || _settings.RightKey == "VolumeUp";
        if (volumeMode)
        {
            text += Strings.T("VolumeModeTitle") + "\r\n" + Strings.T("VolumeMode") + "\r\n" + Strings.T("LongPress") + "\r\n\r\n";
        }
        text += Strings.T("WarningTitle") + "\r\n" + Strings.T("Warning") + "\r\n\r\n";
        text += Strings.T("SetupTitle") + "\r\n" + Strings.T("Setup") + "\r\n\r\n";
        text += Strings.T("Privacy");
        return text;
    }

    private void LoadValuesFromSettings()
    {
        _loadingValues = true;
        try
        {
            FillKeyBox(_leftKeyBox, _settings.LeftKey);
            FillKeyBox(_rightKeyBox, _settings.RightKey);
            _leftKeyValue.Text = Strings.KeyName(_settings.LeftKey);
            _rightKeyValue.Text = Strings.KeyName(_settings.RightKey);
            _backspaceBox.SelectedItem = _settings.BackspaceCount.ToString();
            _enabledBox.Checked = _settings.Enabled;
            _swallowBox.Checked = _settings.Swallow;
            _autoStartBox.Checked = _settings.AutoStart;
        }
        finally
        {
            _loadingValues = false;
        }
    }

    private void FillKeyBox(ComboBox box, string currentKey)
    {
        box.Items.Clear();
        foreach (string key in SelectableKeys)
        {
            box.Items.Add(new KeyItem { Key = key, Display = Strings.KeyName(key) });
        }
        bool found = false;
        foreach (string key in SelectableKeys)
        {
            if (key == currentKey) { found = true; break; }
        }
        if (!found && currentKey != null && currentKey.Length > 0)
        {
            box.Items.Add(new KeyItem { Key = currentKey, Display = Strings.KeyName(currentKey) });
        }
        foreach (object item in box.Items)
        {
            KeyItem keyItem = item as KeyItem;
            if (keyItem != null && keyItem.Key == currentKey)
            {
                box.SelectedItem = item;
                break;
            }
        }
    }

    private void SaveSettings()
    {
        if (_settings.HasConflict())
        {
            ShowError(Strings.T("Conflict"));
            LoadValuesFromSettings();
            return;
        }
        HideError();
        _app.SaveAndApply();
        _leftKeyValue.Text = Strings.KeyName(_settings.LeftKey);
        _rightKeyValue.Text = Strings.KeyName(_settings.RightKey);
        _notesBox.Text = BuildNotesText();
        RefreshLearnButtons();
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    private void HideError()
    {
        _errorLabel.Visible = false;
    }

    private void SetHint(string side, string text)
    {
        if (side == "left") _leftHint.Text = text;
        else _rightHint.Text = text;
    }

    private void RefreshLearnButtons()
    {
        bool busy = _pendingLearn.Length > 0;
        bool running = EarbudEngine.Running;
        _leftLearnButton.Enabled = running && !busy;
        _rightLearnButton.Enabled = running && !busy;
        _leftTestButton.Enabled = running;
        _rightTestButton.Enabled = running;
        _leftKeyBox.Enabled = running;
        _rightKeyBox.Enabled = running;
    }

    private void Learn(string side)
    {
        if (!EarbudEngine.Running)
        {
            ShowError(Strings.T("HelperStopped"));
            return;
        }
        HideError();
        _pendingLearn = side;
        SetHint(side, side == "left" ? Strings.T("LearningLeft") : Strings.T("LearningRight"));
        _learnTimer.Stop();
        _learnTimer.Start();
        EarbudEngine.BeginLearn(side);
        RefreshLearnButtons();
    }

    private void FinishLearn(string side, string key)
    {
        if (_pendingLearn != side) return;
        _learnTimer.Stop();
        _pendingLearn = "";
        string other = side == "left" ? _settings.RightKey : _settings.LeftKey;
        if (key == other)
        {
            SetHint(side, Strings.T("LearnConflict"));
            RefreshLearnButtons();
            return;
        }
        if (side == "left") _settings.LeftKey = key; else _settings.RightKey = key;
        SaveSettings();
        FillKeyBox(side == "left" ? _leftKeyBox : _rightKeyBox, key);
        SetHint(side, Strings.T("LastEvent") + ": " + Strings.KeyName(key));
        RefreshLearnButtons();
    }

    private void Test(string side)
    {
        if (!EarbudEngine.Running)
        {
            ShowError(Strings.T("HelperStopped"));
            return;
        }
        string action = side == "left" ? _settings.LeftAction : _settings.RightAction;
        int delayMs = action == "voice" ? 2500 : 250;
        EarbudEngine.TestAction(side, delayMs);
        SetHint(side, action == "voice" ? Strings.T("VoiceTestHint") : Strings.T("BackspaceTestHint"));
    }

    private void OnEngineLine(string json)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action<string>(OnEngineLine), json);
            return;
        }
        string type = EarbudEngine.GetString(json, "type");
        if (type == "learned")
        {
            FinishLearn(EarbudEngine.GetString(json, "side"), EarbudEngine.GetString(json, "key"));
        }
        else if (type == "learning")
        {
            string side = EarbudEngine.GetString(json, "side");
            SetHint(side, side == "left" ? Strings.T("LearningLeft") : Strings.T("LearningRight"));
        }
        else if (type == "error")
        {
            ShowError(EarbudEngine.GetString(json, "message"));
        }
        else if (type == "ready" || type == "configured" || type == "stopped")
        {
            RefreshStatus();
        }
    }

    public void RefreshStatus()
    {
        if (this.IsDisposed) return;
        bool running = EarbudEngine.Running;
        _dotLabel.ForeColor = running ? Color.Green : Color.Gray;
        _statusLabel.Text = running ? Strings.T("Listening") : Strings.T("Stopped");
        string sub;
        if (running)
        {
            if (_settings.HasConflict())
            {
                sub = Strings.T("Conflict");
            }
            else if (_app.LastEventText.Length > 0)
            {
                sub = Strings.T("LastEvent") + ": " + _app.LastEventText;
            }
            else
            {
                sub = Strings.T("NoEvent");
            }
            if (_app.VolumeLevel >= 0)
            {
                sub += "  ·  " + Strings.T("VolumeLevel") + " " + ((int)(_app.VolumeLevel * 100)).ToString() + "%";
            }
            if (!EarbudEngine.HookInstalled)
            {
                sub += "  ·  " + Strings.T("HookFail");
            }
        }
        else
        {
            sub = _app.LastError.Length > 0 ? _app.LastError : Strings.T("HelperStopped");
        }
        _statusSubLabel.Text = sub;
        string[] recent = _app.RecentEvents();
        _recentBox.BeginUpdate();
        _recentBox.Items.Clear();
        foreach (string item in recent) _recentBox.Items.Add(item);
        _recentBox.EndUpdate();
        RefreshLearnButtons();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_app.IsExiting)
        {
            e.Cancel = true;
            this.Hide();
            _app.HideSettings();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EarbudEngine.Line -= OnEngineLine;
            if (_learnTimer != null) _learnTimer.Dispose();
            if (_statusTimer != null) _statusTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
