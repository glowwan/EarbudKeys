// EarbudEngine: the Windows detection engine of the standalone
// "Bluetooth earbud keys" app (EarbudKeys).
//
// Adapted from helper/EarbudHelper.cs of glowwan/dsh-bluetooth-earbuds
// (MIT). The detection logic is unchanged: a WH_KEYBOARD_LL hook, a Raw
// Input window, GetAsyncKeyState polling, and Core Audio volume-change
// detection. What changed is the hosting: instead of running as a child
// process that talks JSON over stdin/stdout to a DSH host, the engine now
// lives in-process and raises the same JSON lines as a C# event, so the
// tray app can drive it directly. Targets .NET Framework 4.x / C# 5 so it
// still compiles with the in-box csc.exe (no SDK needed).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal static class EarbudEngine
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIM_TYPEKEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint INPUT_KEYBOARD = 1;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_H = 0x48;
    private const uint VK_BACK = 0x08;
    private const uint VK_RETURN = 0x0D;
    private const uint VK_SPACE = 0x20;
    private const uint VK_TAB = 0x09;
    private const uint VK_ESCAPE = 0x1B;
    private const uint VK_DELETE = 0x2E;
    private const ulong INJECT_TAG = 0x44534845A1B2C3D4UL;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWKEYBOARD keyboard;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(uint dwAccess, out IntPtr ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out uint pdwState);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    /// <summary>Raised for every engine line (same JSON format as the helper process).</summary>
    public static event Action<string> Line;

    private static readonly object Gate = new object();
    private static BlockingCollection<Action> Actions = new BlockingCollection<Action>();
    private static readonly LowLevelKeyboardProc HookDelegate = HookCallback;
    private static IntPtr _hook = IntPtr.Zero;
    private static volatile bool _hookInstalled;
    private static RawInputWindow _rawWindow;
    private static volatile bool _rawInstalled;
    private static volatile bool _volumeAvailable;

    private static string _leftKey = "MediaPlayPause";
    private static string _rightKey = "MediaNextTrack";
    private static string _leftAction = "voice";
    private static string _rightAction = "backspace";
    private static int _leftVk = 0xB3;
    private static int _rightVk = 0xB0;
    private static int _backspaceCount = 1;
    private static volatile bool _swallow = true;
    private static volatile bool _enabled = true;
    private static volatile bool _learning = false;
    private static string _learnSide = "";

    private static int _pendingVk;
    private static int _lastVk;
    private static DateTime _lastHandledUtc = DateTime.MinValue;
    private static bool _stopping = false;
    private static bool _started = false;
    private static Thread _actionThread;
    private static Thread _keyThread;
    private static Thread _pollThread;

    private static readonly int[] MediaKeys = new int[]
    {
        0xB3, 0xB0, 0xB1, 0xB2, 0xB5, 0xAD, 0xAE, 0xAF, 0xA6, 0xA7,
    };

    public static bool Running { get { return _started && !_stopping; } }
    public static bool HookInstalled { get { return _hookInstalled; } }
    public static bool RawInstalled { get { return _rawInstalled; } }
    public static bool VolumeAvailable { get { return _volumeAvailable; } }
    public static int Pid { get { return Process.GetCurrentProcess().Id; } }
    public static string LastError { get; private set; }
    public static bool Learning { get { return _learning; } }

    /// <summary>
    /// Starts the engine: installs the low-level hook and the Raw Input
    /// window, then starts the worker threads. Must be called on the UI
    /// thread (the message-only window and the hook live on it, and the
    /// WinForms message loop pumps them). Returns false when a step fails;
    /// see LastError. Safe to call again after Stop().
    /// </summary>
    public static bool Start()
    {
        if (_started) return true;
        LastError = null;
        _stopping = false;
        _lastVk = 0;
        _lastHandledUtc = DateTime.MinValue;
        lock (Gate)
        {
            _learning = false;
            _learnSide = "";
            if (Actions.IsAddingCompleted)
            {
                Actions = new BlockingCollection<Action>();
            }
        }

        try
        {
            _actionThread = new Thread(ActionLoop);
            _actionThread.IsBackground = true;
            _actionThread.Start();
            _keyThread = new Thread(KeyLoop);
            _keyThread.IsBackground = true;
            _keyThread.Start();
            _pollThread = new Thread(PollLoop);
            _pollThread.IsBackground = true;
            _pollThread.Start();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _stopping = true;
            return false;
        }

        try
        {
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, HookDelegate, GetModuleHandle(null), 0);
            _hookInstalled = _hook != IntPtr.Zero;
            if (!_hookInstalled)
            {
                LastError = "Could not install the keyboard hook (error " + Marshal.GetLastWin32Error().ToString() + ").";
            }
        }
        catch (Exception ex)
        {
            _hookInstalled = false;
            LastError = "Could not install the keyboard hook: " + ex.Message;
        }

        // Raw Input reads the physical keyboard device directly and is not part
        // of the low-level hook chain that Chromium can silence.
        try
        {
            _rawWindow = new RawInputWindow();
            var create = new CreateParams();
            create.Caption = "EarbudKeysRawInput";
            create.Parent = (IntPtr)(-3); // HWND_MESSAGE
            _rawWindow.CreateHandle(create);
            var device = new RAWINPUTDEVICE();
            device.usUsagePage = 0x01;
            device.usUsage = 0x06;
            device.dwFlags = RIDEV_INPUTSINK;
            device.hwndTarget = _rawWindow.Handle;
            _rawInstalled = RegisterRawInputDevices(new RAWINPUTDEVICE[] { device }, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }
        catch
        {
            _rawInstalled = false;
        }

        _started = true;
        Emit("{\"type\":\"ready\",\"pid\":" + Pid.ToString()
            + ",\"hook\":" + (_hookInstalled ? "true" : "false")
            + ",\"raw\":" + (_rawInstalled ? "true" : "false")
            + ",\"volume\":" + (_volumeAvailable ? "true" : "false")
            + ",\"hookError\":" + (_hookInstalled ? "0" : Marshal.GetLastWin32Error().ToString()) + "}");
        return true;
    }

    /// <summary>Stops the engine: unhooks, destroys the Raw Input window, stops worker threads.</summary>
    public static void Stop()
    {
        if (!_started) return;
        lock (Gate)
        {
            if (_stopping) return;
            _stopping = true;
            _learning = false;
            _learnSide = "";
        }
        try { Actions.CompleteAdding(); } catch { }
        // Wait for the polling threads to exit before a later Start() spawns
        // new ones; otherwise two poll loops would fight over the volume.
        // (The action thread is not joined: it may be sleeping in a delayed
        // test action, and it is a background thread that exits on its own.)
        try { if (_keyThread != null) _keyThread.Join(1000); } catch { }
        try { if (_pollThread != null) _pollThread.Join(1000); } catch { }
        try
        {
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        }
        catch { }
        _hook = IntPtr.Zero;
        _hookInstalled = false;
        try { if (_rawWindow != null) _rawWindow.DestroyHandle(); }
        catch { }
        _rawWindow = null;
        _rawInstalled = false;
        _started = false;
        Emit("{\"type\":\"stopped\"}");
    }

    /// <summary>Applies a config JSON object (same fields as the plugin config).</summary>
    public static void ApplyConfigJson(string json)
    {
        ApplyConfig(json);
        Emit("{\"type\":\"configured\",\"leftKey\":" + Json(_leftKey) + ",\"rightKey\":" + Json(_rightKey)
            + ",\"enabled\":" + (_enabled ? "true" : "false")
            + ",\"hook\":" + (_hookInstalled ? "true" : "false")
            + ",\"conflict\":" + (_leftVk == _rightVk ? "true" : "false") + "}");
    }

    /// <summary>Starts listening for the next physical earbud key for one side.</summary>
    public static void BeginLearn(string side)
    {
        lock (Gate)
        {
            _learnSide = side == "right" ? "right" : "left";
            _learning = true;
            Emit("{\"type\":\"learning\",\"side\":" + Json(_learnSide) + "}");
        }
    }

    public static void CancelLearn()
    {
        lock (Gate)
        {
            _learning = false;
            _learnSide = "";
        }
        Emit("{\"type\":\"learn-cancelled\"}");
    }

    /// <summary>Performs the configured action for one side, after delayMs.</summary>
    public static void TestAction(string side, int delayMs)
    {
        string action;
        int count;
        lock (Gate)
        {
            action = side == "right" ? _rightAction : _leftAction;
            count = _backspaceCount;
        }
        if (action == "none") action = side == "right" ? "backspace" : "voice";
        string captured = action;
        int capturedCount = count;
        int capturedDelay = delayMs;
        Actions.Add(delegate { if (capturedDelay > 0) Thread.Sleep(capturedDelay); Perform(captured, capturedCount); });
    }

    /// <summary>Injects the configured media key for one side (tag 0, so the hook treats it like an earbud key).</summary>
    public static void Simulate(string side)
    {
        string key;
        lock (Gate)
        {
            key = side == "right" ? _rightKey : _leftKey;
        }
        int vk = VkForKeyName(key);
        if (vk < 0)
        {
            Emit("{\"type\":\"error\",\"message\":" + Json("Cannot simulate an unknown key: " + key) + "}");
            return;
        }
        int simulatedVk = vk;
        string simulatedSide = side == "right" ? "right" : "left";
        Actions.Add(delegate
        {
            Thread.Sleep(80);
            SendRawKey(simulatedVk);
            Emit("{\"type\":\"simulated\",\"side\":" + Json(simulatedSide) + ",\"key\":" + Json(KeyName(simulatedVk)) + ",\"vk\":" + simulatedVk.ToString() + "}");
        });
    }

    private sealed class RawInputWindow : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT)
            {
                uint size = 0;
                uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
                GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
                if (size > 0 && size <= 512)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)size);
                    try
                    {
                        uint read = GetRawInputData(m.LParam, RID_INPUT, buffer, ref size, headerSize);
                        if (read == size)
                        {
                            var raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                            if (raw.header.dwType == RIM_TYPEKEYBOARD
                                && (raw.keyboard.Message == WM_KEYDOWN || raw.keyboard.Message == WM_SYSKEYDOWN)
                                && (raw.keyboard.Flags & 1) == 0)
                            {
                                SubmitKey(raw.keyboard.VKey);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed input records.
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            base.WndProc(ref m);
        }
    }

    // Keep this callback microscopic: no locks, strings, logging, or allocations.
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            if (info.dwExtraInfo.ToUInt64() != INJECT_TAG)
            {
                int vk = (int)info.vkCode;
                bool learning = _learning;
                bool mine = _enabled && (vk == _leftVk || vk == _rightVk);
                if (learning || mine)
                {
                    SubmitKey(vk);
                    if (!learning && mine && _swallow) return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void SubmitKey(int vk)
    {
        if (vk <= 0) return;
        Interlocked.CompareExchange(ref _pendingVk, vk, 0);
    }

    private static void KeyLoop()
    {
        while (!_stopping)
        {
            int vk = Interlocked.Exchange(ref _pendingVk, 0);
            if (vk != 0)
            {
                try
                {
                    HandleKey(vk);
                }
                catch (Exception ex)
                {
                    Emit("{\"type\":\"error\",\"message\":" + Json("Key handling failed: " + ex.Message) + "}");
                }
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    private static void PollLoop()
    {
        var keys = new List<int>(MediaKeys);
        lock (Gate)
        {
            if (!keys.Contains(_leftVk)) keys.Add(_leftVk);
            if (!keys.Contains(_rightVk)) keys.Add(_rightVk);
        }
        var down = new Dictionary<int, bool>();
        foreach (int key in keys) down[key] = false;

        IAudioEndpointVolume volume = null;
        try
        {
            CoInitializeEx(IntPtr.Zero, 2);
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice device;
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) == 0 && device != null)
            {
                Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                object endpoint;
                if (device.Activate(ref iid, 23, IntPtr.Zero, out endpoint) == 0)
                {
                    volume = endpoint as IAudioEndpointVolume;
                }
            }
        }
        catch
        {
            volume = null;
        }
        _volumeAvailable = volume != null;
        Emit("{\"type\":\"capabilities\",\"volume\":" + (_volumeAvailable ? "true" : "false") + "}");

        bool volumeInitialized = false;
        float volumeLast = 0;
        float volumeReported = -1;
        DateTime nextVolumeReport = DateTime.MinValue;
        float gestureStart = 0;
        bool gestureActive = false;
        DateTime gestureChanged = DateTime.MinValue;
        DateTime cooldownUntil = DateTime.MinValue;
        DateTime nextVolumeCheck = DateTime.MinValue;
        bool keyPollingBroken = false;

        while (!_stopping)
        {
            bool enabled = _enabled;
            bool learning = _learning;
            if ((enabled || learning) && !keyPollingBroken)
            {
                try
                {
                    foreach (int key in keys)
                    {
                        short state = GetAsyncKeyState(key);
                        bool isDown = (state & 0x8000) != 0;
                        bool wasPressed = (state & 0x0001) != 0;
                        bool wasDown;
                        down.TryGetValue(key, out wasDown);
                        if ((isDown && !wasDown) || (wasPressed && !isDown && !wasDown))
                        {
                            SubmitKey(key);
                        }
                        down[key] = isDown;
                    }
                }
                catch (Exception ex)
                {
                    // A missing/broken key API (e.g. not Windows) must not kill
                    // the app: give up key polling and keep the other paths.
                    keyPollingBroken = true;
                    Emit("{\"type\":\"error\",\"message\":" + Json("Key polling failed: " + ex.Message) + "}");
                }
            }

            if (volume != null && DateTime.UtcNow >= nextVolumeCheck)
            {
                try
                {
                nextVolumeCheck = DateTime.UtcNow.AddMilliseconds(35);
                float level;
                if (volume.GetMasterVolumeLevelScalar(out level) == 0)
                {
                    if (!volumeInitialized)
                    {
                        volumeInitialized = true;
                        volumeLast = level;
                        volumeReported = level;
                        Emit("{\"type\":\"volume\",\"level\":" + level.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "}");
                        nextVolumeReport = DateTime.UtcNow.AddMilliseconds(700);
                    }
                    else
                    {
                        if (Math.Abs(level - volumeReported) > 0.001f && DateTime.UtcNow >= nextVolumeReport)
                        {
                            volumeReported = level;
                            Emit("{\"type\":\"volume\",\"level\":" + level.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "}");
                            nextVolumeReport = DateTime.UtcNow.AddMilliseconds(700);
                        }

                        if (DateTime.UtcNow < cooldownUntil)
                        {
                            // A slow three-tap can produce more than one volume
                            // burst. Ignore bursts during the cooldown and keep
                            // the user's original volume stable.
                            if (Math.Abs(level - volumeLast) > 0.0025f)
                            {
                                Guid context = Guid.Empty;
                                volume.SetMasterVolumeLevelScalar(gestureStart, ref context);
                                volumeLast = gestureStart;
                            }
                        }
                        else
                        {
                            float delta = level - volumeLast;
                            if (Math.Abs(delta) > 0.0025f)
                            {
                                if (!gestureActive)
                                {
                                    gestureActive = true;
                                    gestureStart = volumeLast;
                                }
                                volumeLast = level;
                                gestureChanged = DateTime.UtcNow;
                            }
                            else if (gestureActive && (DateTime.UtcNow - gestureChanged).TotalMilliseconds > 480)
                            {
                                gestureActive = false;
                                float net = level - gestureStart;
                                int direction = net < -0.004f ? -1 : net > 0.004f ? 1 : 0;
                                if (direction != 0)
                                {
                                    int vk = direction < 0 ? 0xAE : 0xAF;
                                    bool mapped;
                                    lock (Gate)
                                    {
                                        mapped = _leftVk == vk || _rightVk == vk;
                                    }
                                    if (_learning || (_enabled && mapped))
                                    {
                                        HandleKey(vk);
                                        // Use the volume keys as gestures, then put the
                                        // user's volume back where it was.
                                        Guid context = Guid.Empty;
                                        volume.SetMasterVolumeLevelScalar(gestureStart, ref context);
                                        volumeLast = gestureStart;
                                        cooldownUntil = DateTime.UtcNow.AddMilliseconds(1600);
                                    }
                                    else
                                    {
                                        volumeLast = level;
                                    }
                                }
                            }
                        }
                    }
                }
                }
                catch (Exception ex)
                {
                    // A broken audio path (device gone, COM failure) must not
                    // kill the app: give up volume detection and keep polling keys.
                    volume = null;
                    _volumeAvailable = false;
                    Emit("{\"type\":\"error\",\"message\":" + Json("Volume detection failed: " + ex.Message) + "}");
                }
            }

            Thread.Sleep(12);
        }

        try { CoUninitialize(); }
        catch { }
    }

    private static void HandleKey(int vk)
    {
        string name = KeyName(vk);
        bool learning;
        string learnSide;
        lock (Gate)
        {
            learning = _learning;
            learnSide = _learnSide;
            if (learning)
            {
                _learning = false;
                _learnSide = "";
            }
        }

        if (learning)
        {
            Emit("{\"type\":\"learned\",\"side\":" + Json(learnSide) + ",\"key\":" + Json(name) + ",\"vk\":" + vk.ToString() + "}");
            return;
        }

        if (!_enabled) return;

        bool isLeft;
        bool isRight;
        string action;
        int count;
        lock (Gate)
        {
            isLeft = vk == _leftVk;
            isRight = vk == _rightVk;
            if (!isLeft && !isRight) return;
            action = isLeft ? _leftAction : _rightAction;
            count = _backspaceCount;
        }

        var now = DateTime.UtcNow;
        if (vk == _lastVk && (now - _lastHandledUtc).TotalMilliseconds < 160) return;
        _lastVk = vk;
        _lastHandledUtc = now;

        string side = isLeft ? "left" : "right";
        Emit("{\"type\":\"event\",\"side\":" + Json(side) + ",\"key\":" + Json(name) + ",\"vk\":" + vk.ToString() + ",\"action\":" + Json(action) + "}");
        if (action != "none")
        {
            string captured = action;
            int capturedCount = count;
            Actions.Add(delegate { Thread.Sleep(70); Perform(captured, capturedCount); });
        }
    }

    private static void ApplyConfig(string json)
    {
        lock (Gate)
        {
            if (json != null)
            {
                string value = GetString(json, "leftKey");
                if (value.Length > 0) _leftKey = value;
                value = GetString(json, "rightKey");
                if (value.Length > 0) _rightKey = value;
                value = GetString(json, "leftAction");
                if (value.Length > 0) _leftAction = value;
                value = GetString(json, "rightAction");
                if (value.Length > 0) _rightAction = value;
                _backspaceCount = Math.Max(1, Math.Min(20, GetInt(json, "backspaceCount", _backspaceCount)));
                string swallow = GetRaw(json, "swallow");
                if (swallow == "true" || swallow == "false") _swallow = swallow == "true";
                string enabled = GetRaw(json, "enabled");
                if (enabled == "true" || enabled == "false") _enabled = enabled == "true";
            }
            _leftVk = VkForKeyName(_leftKey);
            _rightVk = VkForKeyName(_rightKey);
            if (_leftVk < 0) _leftVk = 0xB3;
            if (_rightVk < 0) _rightVk = 0xB0;
        }
    }

    private static void Perform(string action, int count)
    {
        try
        {
            if (action == "voice") SendWinH();
            else if (action == "backspace") SendVk(VK_BACK, count);
            else if (action == "enter") SendVk(VK_RETURN, 1);
            else if (action == "space") SendVk(VK_SPACE, 1);
            else if (action == "tab") SendVk(VK_TAB, 1);
            else if (action == "escape") SendVk(VK_ESCAPE, 1);
            else if (action == "delete") SendVk(VK_DELETE, count);
        }
        catch (Exception ex)
        {
            Emit("{\"type\":\"error\",\"message\":" + Json("Action failed: " + ex.Message) + "}");
        }
    }

    private static void SendVk(uint vk, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var inputs = new INPUT[2];
            inputs[0] = KeyInput(vk, false, INJECT_TAG);
            inputs[1] = KeyInput(vk, true, INJECT_TAG);
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length)
            {
                Emit("{\"type\":\"error\",\"message\":\"SendInput failed (error " + Marshal.GetLastWin32Error().ToString() + ")\"}");
                return;
            }
            if (count > 1) Thread.Sleep(18);
        }
    }

    private static void SendWinH()
    {
        var inputs = new INPUT[4];
        inputs[0] = KeyInput(VK_LWIN, false, INJECT_TAG);
        inputs[1] = KeyInput(VK_H, false, INJECT_TAG);
        inputs[2] = KeyInput(VK_H, true, INJECT_TAG);
        inputs[3] = KeyInput(VK_LWIN, true, INJECT_TAG);
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length)
        {
            Emit("{\"type\":\"error\",\"message\":\"SendInput for Win+H failed (error " + Marshal.GetLastWin32Error().ToString() + ")\"}");
        }
    }

    // A simulated test key has tag 0, so the hook/poller treat it like an earbud key.
    private static void SendRawKey(int vk)
    {
        var inputs = new INPUT[2];
        inputs[0] = KeyInput((uint)vk, false, 0);
        inputs[1] = KeyInput((uint)vk, true, 0);
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        if (sent != inputs.Length)
        {
            Emit("{\"type\":\"error\",\"message\":\"Simulated key could not be sent (error " + Marshal.GetLastWin32Error().ToString() + ").\"}");
        }
    }

    private static INPUT KeyInput(uint vk, bool up, ulong tag)
    {
        var input = new INPUT();
        input.type = INPUT_KEYBOARD;
        input.u.ki.wVk = (ushort)vk;
        input.u.ki.wScan = 0;
        input.u.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
        input.u.ki.time = 0;
        input.u.ki.dwExtraInfo = new IntPtr(unchecked((long)tag));
        return input;
    }

    private static void ActionLoop()
    {
        foreach (var action in Actions.GetConsumingEnumerable())
        {
            try { action(); }
            catch (Exception ex) { Emit("{\"type\":\"error\",\"message\":" + Json(ex.Message) + "}"); }
        }
    }

    private static void Emit(string json)
    {
        try
        {
            Action<string> handler = Line;
            if (handler != null) handler(json);
        }
        catch { }
    }

    internal static string GetString(string json, string name)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        if (!match.Success) return "";
        return Regex.Unescape(match.Groups[1].Value);
    }

    internal static string GetRaw(string json, string name)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(true|false|-?[0-9]+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    internal static int GetInt(string json, string name, int fallback)
    {
        var raw = GetRaw(json, name);
        int value;
        return int.TryParse(raw, out value) ? value : fallback;
    }

    internal static double GetDouble(string json, string name, double fallback)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(-?[0-9]*\\.?[0-9]+)");
        if (!match.Success) return fallback;
        double value;
        if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return value;
        return fallback;
    }

    internal static string Json(string value)
    {
        if (value == null) return "null";
        var b = new StringBuilder("\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\r': b.Append("\\r"); break;
                case '\n': b.Append("\\n"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                    else b.Append(c);
                    break;
            }
        }
        return b.Append('"').ToString();
    }

    internal static int VkForKeyName(string name)
    {
        switch (name)
        {
            case "MediaPlayPause": return 0xB3;
            case "MediaNextTrack": return 0xB0;
            case "MediaPrevTrack": return 0xB1;
            case "MediaStop": return 0xB2;
            case "MediaSelect": return 0xB5;
            case "VolumeMute": return 0xAD;
            case "VolumeDown": return 0xAE;
            case "VolumeUp": return 0xAF;
            case "BrowserBack": return 0xA6;
            case "BrowserForward": return 0xA7;
            case "Enter": return 0x0D;
            case "Space": return 0x20;
            case "Tab": return 0x09;
            case "Escape": return 0x1B;
            case "Backspace": return 0x08;
            case "Delete": return 0x2E;
            default:
                if (name.StartsWith("vk:", StringComparison.OrdinalIgnoreCase))
                {
                    string raw = name.Substring(3);
                    int parsed;
                    if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(raw.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out parsed)) return parsed;
                    }
                    else if (int.TryParse(raw, out parsed)) return parsed;
                }
                return -1;
        }
    }

    internal static string KeyName(int vk)
    {
        switch (vk)
        {
            case 0xB3: return "MediaPlayPause";
            case 0xB0: return "MediaNextTrack";
            case 0xB1: return "MediaPrevTrack";
            case 0xB2: return "MediaStop";
            case 0xB5: return "MediaSelect";
            case 0xAD: return "VolumeMute";
            case 0xAE: return "VolumeDown";
            case 0xAF: return "VolumeUp";
            case 0xA6: return "BrowserBack";
            case 0xA7: return "BrowserForward";
            case 0x0D: return "Enter";
            case 0x20: return "Space";
            case 0x09: return "Tab";
            case 0x1B: return "Escape";
            case 0x08: return "Backspace";
            case 0x2E: return "Delete";
            default: return "vk:" + vk.ToString();
        }
    }
}
