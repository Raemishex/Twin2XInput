using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Twin2XInput.Config;
using Twin2XInput.Native;

namespace Twin2XInput.Bridge
{
    internal sealed class ControllerBridge : IDisposable
    {
        private const int MinimumBoostedRumble = 65;
        private const int VibrationKeepAliveMs = 200;

        public readonly byte ReportId;
        public readonly string HidPath;
        public uint XInputSlot = 99;
        public string LiveSummary = "Waiting for input...";
        public Action<byte, byte> OnScaledVibration;

        public byte OutputLeft { get { return _curLeft; } }
        public byte OutputRight { get { return _curRight; } }

        private readonly IntPtr _vigem;
        private readonly BridgeSettings _cfg;
        private readonly object _vibLock = new object();

        private SafeFileHandle _hid;
        private IntPtr _preparsedData = IntPtr.Zero;
        private IntPtr _target = IntPtr.Zero;
        private NativeMethods.VigemX360Notification _notificationCallback;
        private GCHandle _callbackHandle;
        private Thread _workerThread;
        private volatile bool _running;

        private volatile byte _curLeft;
        private volatile byte _curRight;
        private int _centerX = 128, _centerY = 128, _centerZ = 128, _centerRz = 128;
        private uint _lastRawX = 128, _lastRawY = 128, _lastRawZ = 128, _lastRawRz = 128;

        public ControllerBridge(IntPtr vigem, byte reportId, string hidPath, BridgeSettings cfg)
        {
            _vigem = vigem;
            ReportId = reportId;
            HidPath = hidPath;
            _cfg = cfg;
        }

        public bool Start(out string error)
        {
            error = null;
            _hid = NativeMethods.CreateFile(
                HidPath,
                NativeMethods.GenericReadWrite,
                NativeMethods.FileShareReadWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                NativeMethods.FileFlagOverlapped,
                IntPtr.Zero);

            if (_hid.IsInvalid)
            {
                error = "Failed to open HID interface (Win32=" + Marshal.GetLastWin32Error() + ")";
                return false;
            }

            if (!NativeMethods.HidD_GetPreparsedData(_hid, out _preparsedData))
            {
                error = "Failed to read HID preparsed descriptor";
                _hid.Dispose();
                _hid = null;
                return false;
            }

            _target = NativeMethods.vigem_target_x360_alloc();
            uint addResult = NativeMethods.vigem_target_add(_vigem, _target);
            if (addResult != NativeMethods.VigemErrorNone)
            {
                error = string.Format("ViGEm target_add failed: 0x{0:X}", addResult);
                NativeMethods.vigem_target_free(_target);
                _target = IntPtr.Zero;
                NativeMethods.HidD_FreePreparsedData(_preparsedData);
                _preparsedData = IntPtr.Zero;
                _hid.Dispose();
                _hid = null;
                return false;
            }

            Thread.Sleep(150);
            uint slot;
            if (NativeMethods.vigem_target_x360_get_user_index(_vigem, _target, out slot) == NativeMethods.VigemErrorNone)
                XInputSlot = slot;

            _notificationCallback = OnFeedback;
            _callbackHandle = GCHandle.Alloc(_notificationCallback);
            NativeMethods.vigem_target_x360_register_notification(_vigem, _target, _notificationCallback, IntPtr.Zero);

            _running = true;
            _workerThread = new Thread(InputLoop) { IsBackground = true };
            _workerThread.Start();
            return true;
        }

        public void CalibrateCenter()
        {
            _centerX = (int)_lastRawX;
            _centerY = (int)_lastRawY;
            _centerZ = (int)_lastRawZ;
            _centerRz = (int)_lastRawRz;
        }

        public void ResetCenterDefault()
        {
            _centerX = 128;
            _centerY = 128;
            _centerZ = 128;
            _centerRz = 128;
        }

        public void SetManualVibration(byte left, byte right)
        {
            _curLeft = left;
            _curRight = right;
            SendVibrationPacket(left, right);
        }

        private byte ScaleVibration(byte motorValue)
        {
            if (motorValue == 0) return 0;
            int scaled = (int)Math.Round(motorValue * (_cfg.VibBoostPct / 100.0));
            if (_cfg.VibBoostPct > 100 && scaled < MinimumBoostedRumble)
                scaled = MinimumBoostedRumble;
            return (byte)Math.Min(255, scaled);
        }

        private void OnFeedback(IntPtr client, IntPtr target, byte largeMotor, byte smallMotor, byte ledNumber, IntPtr userData)
        {
            byte left = ScaleVibration(largeMotor);
            byte right = ScaleVibration(smallMotor);
            _curLeft = left;
            _curRight = right;
            SendVibrationPacket(left, right);

            var mirror = OnScaledVibration;
            if (mirror != null) mirror(left, right);
        }

        private void SendVibrationPacket(byte left, byte right)
        {
            if (_hid == null || _hid.IsInvalid) return;
            lock (_vibLock)
            {
                byte[] packet = new byte[] { ReportId, 0x00, 0x00, left, right };
                uint written;
                NativeMethods.WriteFile(_hid, packet, 5, out written, IntPtr.Zero);
                NativeMethods.HidD_SetOutputReport(_hid, packet, 5);
            }
        }

        private void InputLoop()
        {
            IntPtr readEvent = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
            byte[] buffer = new byte[8];
            ushort[] usages = new ushort[16];
            long lastKeepAlive = Environment.TickCount;

            try
            {
                while (_running)
                {
                    uint bytesRead;
                    if (TryReadHidPacket(readEvent, buffer, out bytesRead) && bytesRead >= 8 && IsMatchingReport(buffer[0]))
                    {
                        ProcessInputPacket(buffer, bytesRead, usages);
                    }

                    long now = Environment.TickCount;
                    if ((_curLeft > 0 || _curRight > 0) && unchecked(now - lastKeepAlive) >= VibrationKeepAliveMs)
                    {
                        SendVibrationPacket(_curLeft, _curRight);
                        lastKeepAlive = now;
                    }
                }
            }
            finally
            {
                NativeMethods.CloseHandle(readEvent);
            }
        }

        private bool IsMatchingReport(byte incomingId)
        {
            return incomingId == ReportId || (ReportId == 1 && incomingId == 0);
        }

        private bool TryReadHidPacket(IntPtr readEvent, byte[] buffer, out uint bytesRead)
        {
            var overlapped = new NativeOverlapped { EventHandle = readEvent };
            bool ok = NativeMethods.ReadFile(_hid, buffer, (uint)buffer.Length, out bytesRead, ref overlapped);
            int err = Marshal.GetLastWin32Error();

            if (!ok && err == NativeMethods.ErrorIoPending)
            {
                if (NativeMethods.WaitForSingleObject(readEvent, 25) == NativeMethods.WaitObject0)
                    ok = NativeMethods.GetOverlappedResult(_hid, ref overlapped, out bytesRead, false);
                else
                    NativeMethods.CancelIoEx(_hid, IntPtr.Zero);
            }

            return ok;
        }

        private void ProcessInputPacket(byte[] buffer, uint bytesRead, ushort[] usages)
        {
            uint rx = 128, ry = 128, rz = 128, rrz = 128, hat = 15;
            NativeMethods.HidP_GetUsageValue(0, 1, 0, 0x30, out rx, _preparsedData, buffer, bytesRead);
            NativeMethods.HidP_GetUsageValue(0, 1, 0, 0x31, out ry, _preparsedData, buffer, bytesRead);
            NativeMethods.HidP_GetUsageValue(0, 1, 0, 0x32, out rz, _preparsedData, buffer, bytesRead);
            NativeMethods.HidP_GetUsageValue(0, 1, 0, 0x35, out rrz, _preparsedData, buffer, bytesRead);
            NativeMethods.HidP_GetUsageValue(0, 1, 0, 0x39, out hat, _preparsedData, buffer, bytesRead);

            _lastRawX = rx;
            _lastRawY = ry;
            _lastRawZ = rz;
            _lastRawRz = rrz;

            uint usageCount = (uint)usages.Length;
            NativeMethods.HidP_GetUsages(0, 9, 0, usages, ref usageCount, _preparsedData, buffer, bytesRead);

            ushort buttons = DecodeDpad(hat);
            byte leftTrigger = 0;
            byte rightTrigger = 0;
            var activeLabels = new List<string>();

            MapButtons(usages, usageCount, ref buttons, ref leftTrigger, ref rightTrigger, activeLabels);

            var report = BuildXboxReport(rx, ry, rz, rrz, buttons, leftTrigger, rightTrigger);
            NativeMethods.vigem_target_x360_update(_vigem, _target, report);

            LiveSummary = string.Format(
                "XInput #{0} | L=({1},{2}) R=({3},{4}) | Raw=[X:{5} Y:{6} Z:{7} Rz:{8}] | Btn: {9}",
                XInputSlot,
                report.sThumbLX, report.sThumbLY,
                report.sThumbRX, report.sThumbRY,
                rx, ry, rz, rrz,
                activeLabels.Count > 0 ? string.Join("+", activeLabels.ToArray()) : "-");
        }

        private NativeMethods.XUSB_REPORT BuildXboxReport(
            uint rx, uint ry, uint rz, uint rrz, ushort buttons, byte leftTrigger, byte rightTrigger)
        {
            uint rawRX = _cfg.SwapRightAxes ? rrz : rz;
            uint rawRY = _cfg.SwapRightAxes ? rz : rrz;
            int centerRX = _cfg.SwapRightAxes ? _centerRz : _centerZ;
            int centerRY = _cfg.SwapRightAxes ? _centerZ : _centerRz;

            return new NativeMethods.XUSB_REPORT
            {
                wButtons = buttons,
                bLeftTrigger = leftTrigger,
                bRightTrigger = rightTrigger,
                sThumbLX = MapAxis(rx, _centerX, false),
                sThumbLY = MapAxis(ry, _centerY, _cfg.InvertLY),
                sThumbRX = MapAxis(rawRX, centerRX, false),
                sThumbRY = MapAxis(rawRY, centerRY, _cfg.InvertRY)
            };
        }

        private static ushort DecodeDpad(uint hat)
        {
            if (hat > 7) return 0;
            ushort dpad = 0;
            if (hat == 7 || hat == 0 || hat == 1) dpad |= 0x0001;
            if (hat >= 1 && hat <= 3) dpad |= 0x0008;
            if (hat >= 3 && hat <= 5) dpad |= 0x0002;
            if (hat >= 5 && hat <= 7) dpad |= 0x0004;
            return dpad;
        }

        private void MapButtons(
            ushort[] usages,
            uint count,
            ref ushort buttons,
            ref byte leftTrigger,
            ref byte rightTrigger,
            List<string> labels)
        {
            for (uint i = 0; i < count; i++)
            {
                switch (usages[i])
                {
                    case 1: buttons |= 0x8000; labels.Add("Y"); break;
                    case 2: buttons |= 0x2000; labels.Add("B"); break;
                    case 3: buttons |= 0x1000; labels.Add("A"); break;
                    case 4: buttons |= 0x4000; labels.Add("X"); break;
                    case 5:
                        if (_cfg.SwapBumpersTriggers) { buttons |= 0x0100; labels.Add("LB"); }
                        else { leftTrigger = 255; labels.Add("LT"); }
                        break;
                    case 6:
                        if (_cfg.SwapBumpersTriggers) { buttons |= 0x0200; labels.Add("RB"); }
                        else { rightTrigger = 255; labels.Add("RT"); }
                        break;
                    case 7:
                        if (_cfg.SwapBumpersTriggers) { leftTrigger = 255; labels.Add("LT"); }
                        else { buttons |= 0x0100; labels.Add("LB"); }
                        break;
                    case 8:
                        if (_cfg.SwapBumpersTriggers) { rightTrigger = 255; labels.Add("RT"); }
                        else { buttons |= 0x0200; labels.Add("RB"); }
                        break;
                    case 9: buttons |= 0x0020; labels.Add("Back"); break;
                    case 10: buttons |= 0x0010; labels.Add("Start"); break;
                    case 11: buttons |= 0x0040; labels.Add("L3"); break;
                    case 12: buttons |= 0x0080; labels.Add("R3"); break;
                }
            }
        }

        private short MapAxis(uint raw, int center, bool invert)
        {
            double delta = (int)raw - center;
            double maxSpan = delta >= 0 ? Math.Max(1.0, 255.0 - center) : Math.Max(1.0, center);
            double norm = Math.Max(-1.0, Math.Min(1.0, delta / maxSpan));
            double deadzone = _cfg.DeadzonePct / 100.0;

            if (Math.Abs(norm) < deadzone)
                norm = 0.0;
            else
                norm = Math.Sign(norm) * ((Math.Abs(norm) - deadzone) / Math.Max(0.01, 1.0 - deadzone));

            if (invert) norm = -norm;
            int scaled = (int)Math.Round(norm * 32767.0);
            return (short)Math.Max(-32768, Math.Min(32767, scaled));
        }

        public void Dispose()
        {
            _running = false;
            if (_workerThread != null && _workerThread.IsAlive)
                _workerThread.Join(300);

            SendVibrationPacket(0, 0);

            if (_target != IntPtr.Zero)
            {
                try { NativeMethods.vigem_target_x360_unregister_notification(_target); } catch { }
                try { NativeMethods.vigem_target_remove(_vigem, _target); } catch { }
                try { NativeMethods.vigem_target_free(_target); } catch { }
                _target = IntPtr.Zero;
            }

            if (_callbackHandle.IsAllocated)
                _callbackHandle.Free();

            if (_preparsedData != IntPtr.Zero)
            {
                NativeMethods.HidD_FreePreparsedData(_preparsedData);
                _preparsedData = IntPtr.Zero;
            }

            if (_hid != null)
            {
                _hid.Dispose();
                _hid = null;
            }
        }
    }
}
