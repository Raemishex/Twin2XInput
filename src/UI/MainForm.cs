using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Twin2XInput.Bridge;
using Twin2XInput.Config;
using Twin2XInput.Hid;
using Twin2XInput.Native;

namespace Twin2XInput.UI
{
    internal sealed class MainForm : Form
    {
        private const string VigemBusDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases";

        private readonly BridgeSettings _cfg = new BridgeSettings();
        private IntPtr _vigem = IntPtr.Zero;
        private ControllerBridge _pad1;
        private ControllerBridge _pad2;
        private TwinHidPaths _paths = new TwinHidPaths();

        private Label _lblStatus;
        private LinkLabel _lnkVigem;
        private Label _lblPad1;
        private Label _lblPad2;
        private Label _lblVib;
        private Timer _uiTimer;

        public MainForm()
        {
            _cfg.Load();
            InitializeWindow();
            BuildControls();
            InitializeBridges();
            StartUiRefresh();
        }

        private void InitializeWindow()
        {
            Text = "Twin2XInput — Xbox 360 & Vibration Bridge";
            Size = new Size(575, 470);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
        }

        private void BuildControls()
        {
            _lblStatus = new Label { Location = new Point(15, 12), Size = new Size(530, 22), ForeColor = Color.DarkGreen, UseMnemonic = false };
            _lnkVigem = new LinkLabel
            {
                Text = "ViGEmBus driver missing? Click here to download ViGEmBusSetup_x64.msi",
                Location = new Point(15, 34),
                Size = new Size(530, 20),
                Visible = false
            };
            _lnkVigem.LinkClicked += (s, e) => OpenUrl(VigemBusDownloadUrl);

            _lblPad1 = new Label { Location = new Point(15, 58), Size = new Size(530, 20), Font = new Font("Consolas", 8.8f) };
            _lblPad2 = new Label { Location = new Point(15, 80), Size = new Size(530, 20), Font = new Font("Consolas", 8.8f) };
            _lblVib = new Label { Location = new Point(15, 104), Size = new Size(530, 20), ForeColor = Color.DarkBlue };

            var lblMapping = new Label
            {
                Text = "Button Map (PS -> Xbox): Triangle=Y  Circle=B  Cross=A  Square=X  L1/R1=LB/RB  L2/R2=LT/RT",
                Location = new Point(15, 126),
                Size = new Size(535, 20),
                ForeColor = Color.FromArgb(70, 70, 70),
                Font = new Font("Segoe UI", 8.5f)
            };

            Controls.AddRange(new Control[] { _lblStatus, _lnkVigem, _lblPad1, _lblPad2, _lblVib, lblMapping });

            BuildOptionCheckboxes();
            BuildSliders();
            BuildActionButtons();
        }

        private void BuildOptionCheckboxes()
        {
            var chkSwapR = new CheckBox { Text = "Swap Right Stick (Z <-> Rz)", Checked = _cfg.SwapRightAxes, Location = new Point(15, 154), Size = new Size(245, 24) };
            var chkInvLY = new CheckBox { Text = "Invert Left Stick Y", Checked = _cfg.InvertLY, Location = new Point(15, 180), Size = new Size(245, 24) };
            var chkInvRY = new CheckBox { Text = "Invert Right Stick Y", Checked = _cfg.InvertRY, Location = new Point(15, 206), Size = new Size(245, 24) };

            var chkSwapLt = new CheckBox { Text = "Swap L1/R1 <-> L2/R2", Checked = _cfg.SwapBumpersTriggers, Location = new Point(280, 154), Size = new Size(265, 24) };
            var chkPad2 = new CheckBox { Text = "Enable 2nd Controller (COL02)", Checked = _cfg.EnableSecondPad, Location = new Point(280, 180), Size = new Size(265, 24) };
            var chkMirror = new CheckBox { Text = "Mirror P1 Vibration to P2", Checked = _cfg.MirrorVibToPad2, Location = new Point(280, 206), Size = new Size(265, 24) };

            chkSwapR.CheckedChanged += (s, e) => { _cfg.SwapRightAxes = chkSwapR.Checked; _cfg.Save(); };
            chkInvLY.CheckedChanged += (s, e) => { _cfg.InvertLY = chkInvLY.Checked; _cfg.Save(); };
            chkInvRY.CheckedChanged += (s, e) => { _cfg.InvertRY = chkInvRY.Checked; _cfg.Save(); };
            chkSwapLt.CheckedChanged += (s, e) => { _cfg.SwapBumpersTriggers = chkSwapLt.Checked; _cfg.Save(); };
            chkPad2.CheckedChanged += (s, e) => { _cfg.EnableSecondPad = chkPad2.Checked; _cfg.Save(); TogglePad2(); };
            chkMirror.CheckedChanged += (s, e) =>
            {
                _cfg.MirrorVibToPad2 = chkMirror.Checked;
                _cfg.Save();
                if (!_cfg.MirrorVibToPad2 && _pad2 != null) _pad2.SetManualVibration(0, 0);
            };

            Controls.AddRange(new Control[] { chkSwapR, chkInvLY, chkInvRY, chkSwapLt, chkPad2, chkMirror });
        }

        private void BuildSliders()
        {
            var lblDz = new Label { Text = "Deadzone: " + _cfg.DeadzonePct + "%", Location = new Point(15, 242), Size = new Size(245, 22) };
            var trkDz = new TrackBar { Minimum = 0, Maximum = 40, Value = _cfg.DeadzonePct, TickFrequency = 5, Location = new Point(15, 265), Size = new Size(245, 40) };
            trkDz.Scroll += (s, e) =>
            {
                _cfg.DeadzonePct = trkDz.Value;
                lblDz.Text = "Deadzone: " + _cfg.DeadzonePct + "%";
                _cfg.Save();
            };

            var lblBoost = new Label { Text = "Vibration Boost: " + _cfg.VibBoostPct + "%", Location = new Point(280, 242), Size = new Size(265, 22) };
            var trkBoost = new TrackBar { Minimum = 100, Maximum = 200, Value = _cfg.VibBoostPct, TickFrequency = 20, Location = new Point(280, 265), Size = new Size(265, 40) };
            trkBoost.Scroll += (s, e) =>
            {
                _cfg.VibBoostPct = trkBoost.Value;
                lblBoost.Text = "Vibration Boost: " + _cfg.VibBoostPct + "%";
                _cfg.Save();
            };

            Controls.AddRange(new Control[] { lblDz, trkDz, lblBoost, trkBoost });
        }

        private void BuildActionButtons()
        {
            var btnCal = new Button { Text = "Calibrate Center (Fix Drift)", Location = new Point(15, 316), Size = new Size(245, 36) };
            btnCal.Click += (s, e) =>
            {
                if (_pad1 != null) _pad1.CalibrateCenter();
                if (_pad2 != null) _pad2.CalibrateCenter();
            };

            var btnResetCal = new Button { Text = "Reset Center (128)", Location = new Point(15, 358), Size = new Size(245, 34) };
            btnResetCal.Click += (s, e) =>
            {
                if (_pad1 != null) _pad1.ResetCenterDefault();
                if (_pad2 != null) _pad2.ResetCenterDefault();
            };

            var btnVib = new Button { Text = "Vibration Test (1s)", Location = new Point(280, 316), Size = new Size(265, 36) };
            btnVib.Click += (s, e) => TriggerVibrationPulse();

            var lblNote = new Label
            {
                Text = "Keep open while playing. Closing restores normal DirectInput. Target: " + _cfg.DeviceMatchToken.ToUpperInvariant(),
                Location = new Point(280, 358),
                Size = new Size(270, 38),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false
            };

            Controls.AddRange(new Control[] { btnCal, btnResetCal, btnVib, lblNote });
        }

        private void TriggerVibrationPulse()
        {
            if (_pad1 != null) _pad1.SetManualVibration(255, 255);
            if (_pad2 != null) _pad2.SetManualVibration(255, 255);

            var pulseTimer = new Timer { Interval = 1000 };
            pulseTimer.Tick += (s, e) =>
            {
                pulseTimer.Stop();
                pulseTimer.Dispose();
                if (_pad1 != null) _pad1.SetManualVibration(0, 0);
                if (_pad2 != null) _pad2.SetManualVibration(0, 0);
            };
            pulseTimer.Start();
        }

        private void InitializeBridges()
        {
            _paths = HidDeviceDiscovery.FindMatchingPaths(_cfg.DeviceMatchToken);
            _vigem = NativeMethods.vigem_alloc();
            uint rc = NativeMethods.vigem_connect(_vigem);
            if (rc != NativeMethods.VigemErrorNone)
            {
                _lblStatus.ForeColor = Color.Red;
                _lblStatus.Text = string.Format("ViGEmBus driver not found (0x{0:X}). Install ViGEmBus first.", rc);
                _lnkVigem.Visible = true;
                return;
            }

            if (_paths.Controller1Path == null)
            {
                _lblStatus.ForeColor = Color.Red;
                _lblStatus.Text = "Controller not found (" + _cfg.DeviceMatchToken.ToUpperInvariant() + "). Plug in USB and reopen.";
                return;
            }

            _pad1 = new ControllerBridge(_vigem, 1, _paths.Controller1Path, _cfg);
            string err;
            if (!_pad1.Start(out err))
            {
                _lblStatus.ForeColor = Color.Red;
                _lblStatus.Text = "Controller 1 error: " + err;
                _pad1.Dispose();
                _pad1 = null;
                return;
            }

            _pad1.OnScaledVibration = (left, right) =>
            {
                if (_cfg.MirrorVibToPad2 && _pad2 != null)
                    _pad2.SetManualVibration(left, right);
            };

            _lblStatus.Text = "Active: Controller 1 -> Xbox 360 (XInput #" + _pad1.XInputSlot + ") + Vibration";
            TogglePad2();
        }

        private void TogglePad2()
        {
            if (_cfg.EnableSecondPad && _pad2 == null && _paths.Controller2Path != null && _vigem != IntPtr.Zero)
            {
                _pad2 = new ControllerBridge(_vigem, 2, _paths.Controller2Path, _cfg);
                string err;
                if (!_pad2.Start(out err))
                {
                    _pad2.Dispose();
                    _pad2 = null;
                }
            }
            else if (!_cfg.EnableSecondPad && _pad2 != null)
            {
                _pad2.Dispose();
                _pad2 = null;
            }
        }

        private void StartUiRefresh()
        {
            _uiTimer = new Timer { Interval = 60 };
            _uiTimer.Tick += (s, e) =>
            {
                _lblPad1.Text = _pad1 != null ? "P1: " + _pad1.LiveSummary : "P1: not connected";
                _lblPad2.Text = _pad2 != null ? "P2: " + _pad2.LiveSummary : "P2: disabled (check 'Enable 2nd Controller')";

                int l1 = _pad1 != null ? _pad1.OutputLeft : 0;
                int r1 = _pad1 != null ? _pad1.OutputRight : 0;
                int l2 = _pad2 != null ? _pad2.OutputLeft : 0;
                int r2 = _pad2 != null ? _pad2.OutputRight : 0;

                _lblVib.Text = _pad2 != null
                    ? string.Format("Active Vibration | P1: L={0} R={1} | P2: L={2} R={3}", l1, r1, l2, r2)
                    : string.Format("Active Vibration (P1): Left={0}/255 | Right={1}/255", l1, r1);
            };
            _uiTimer.Start();
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_uiTimer != null) _uiTimer.Stop();
            if (_pad1 != null) { _pad1.Dispose(); _pad1 = null; }
            if (_pad2 != null) { _pad2.Dispose(); _pad2 = null; }
            if (_vigem != IntPtr.Zero)
            {
                try { NativeMethods.vigem_disconnect(_vigem); } catch { }
                try { NativeMethods.vigem_free(_vigem); } catch { }
                _vigem = IntPtr.Zero;
            }
            base.OnFormClosing(e);
        }
    }
}
