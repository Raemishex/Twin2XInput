param(
    [string]$Vid = "0810",
    [string]$Pid = "0001"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public class HidVibTester {
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid hidGuid);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, int Flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_SetOutputReport(SafeFileHandle HidDeviceObject, byte[] ReportBuffer, int ReportBufferLength);

    private static List<Tuple<byte, SafeFileHandle>> _handles = new List<Tuple<byte, SafeFileHandle>>();

    public static int Init(string matchToken) {
        Close();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, null, IntPtr.Zero, 0x12);
        var did = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) };
        for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref did); i++) {
            int req = 0;
            SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out req, IntPtr.Zero);
            IntPtr buf = Marshal.AllocHGlobal(Math.Max(req, 1024));
            Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
            if (SetupDiGetDeviceInterfaceDetail(set, ref did, buf, req, out req, IntPtr.Zero)) {
                string path = Marshal.PtrToStringUni(IntPtr.Add(buf, 4));
                if (path.IndexOf(matchToken, StringComparison.OrdinalIgnoreCase) >= 0) {
                    byte rid = (byte)(path.IndexOf("col02", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 1);
                    var h = CreateFile(path, 0xC0000000u, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (!h.IsInvalid) _handles.Add(Tuple.Create(rid, h));
                }
            }
            Marshal.FreeHGlobal(buf);
        }
        SetupDiDestroyDeviceInfoList(set);
        return _handles.Count;
    }

    public static void Send(byte b1, byte b2, byte b3, byte b4) {
        foreach (var item in _handles) {
            byte[] pkt = new byte[] { item.Item1, b1, b2, b3, b4 };
            uint w = 0;
            WriteFile(item.Item2, pkt, 5, out w, IntPtr.Zero);
            HidD_SetOutputReport(item.Item2, pkt, 5);
        }
    }

    public static void Close() {
        Send(0, 0, 0, 0);
        foreach (var item in _handles) item.Item2.Dispose();
        _handles.Clear();
    }
}
"@

$token = if ([string]::IsNullOrWhiteSpace($Pid)) { "vid_$($Vid.ToLower())" } else { "vid_$($Vid.ToLower())&pid_$($Pid.ToLower())" }
$count = [HidVibTester]::Init($token)

$form = New-Object System.Windows.Forms.Form
$form.Text = "Twin USB HID Vibration Tester ($count handle(s) - $($token.ToUpper()))"
$form.Size = New-Object System.Drawing.Size(420, 400)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)

$lbl = New-Object System.Windows.Forms.Label
$lbl.Text = "Hold the controller and click a byte pattern (4 sec each):"
$lbl.Location = New-Object System.Drawing.Point(15, 12)
$lbl.Size = New-Object System.Drawing.Size(380, 24)
$form.Controls.Add($lbl)

$status = New-Object System.Windows.Forms.Label
$status.Text = if ($count -gt 0) { "Ready (motors stopped)" } else { "No matching HID device found for $($token.ToUpper())" }
$status.Location = New-Object System.Drawing.Point(15, 325)
$status.Size = New-Object System.Drawing.Size(380, 28)
$status.ForeColor = if ($count -gt 0) { [System.Drawing.Color]::DarkBlue } else { [System.Drawing.Color]::Red }
$form.Controls.Add($status)

$script:cur = $null
$script:ticks = 0

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 200
$timer.Add_Tick({
    if ($script:cur -ne $null) {
        [HidVibTester]::Send($script:cur[0], $script:cur[1], $script:cur[2], $script:cur[3])
        $script:ticks--
        if ($script:ticks -le 0) {
            $script:cur = $null
            $timer.Stop()
            [HidVibTester]::Send(0, 0, 0, 0)
            $status.Text = "Stopped. Ready for next test."
        }
    }
})

$items = @(
    @("1) Test Byte 1: [FF, 00, 00, 00]", @(0xFF, 0, 0, 0)),
    @("2) Test Byte 2: [00, FF, 00, 00]", @(0, 0xFF, 0, 0)),
    @("3) Left Motor (Byte 3): [00, 00, FF, 00]", @(0, 0, 0xFF, 0)),
    @("4) Right Motor (Byte 4): [00, 00, 00, FF]", @(0, 0, 0, 0xFF)),
    @("5) Both Motors: [00, 00, FF, FF]", @(0, 0, 0xFF, 0xFF))
)

$y = 45
foreach ($it in $items) {
    $btn = New-Object System.Windows.Forms.Button
    $btn.Text = $it[0]
    $btn.Location = New-Object System.Drawing.Point(15, $y)
    $btn.Size = New-Object System.Drawing.Size(375, 38)
    $bytes = $it[1]
    $labelText = $it[0]
    $btn.Add_Click({
        $script:cur = $bytes
        $script:ticks = 20
        [HidVibTester]::Send($bytes[0], $bytes[1], $bytes[2], $bytes[3])
        $timer.Start()
        $status.Text = "RUNNING (4s): " + $labelText
    }.GetNewClosure())
    $form.Controls.Add($btn)
    $y += 45
}

$stopBtn = New-Object System.Windows.Forms.Button
$stopBtn.Text = "STOP ALL MOTORS"
$stopBtn.Location = New-Object System.Drawing.Point(15, $y)
$stopBtn.Size = New-Object System.Drawing.Size(375, 36)
$stopBtn.BackColor = [System.Drawing.Color]::LightCoral
$stopBtn.Add_Click({
    $script:cur = $null
    $timer.Stop()
    [HidVibTester]::Send(0, 0, 0, 0)
    $status.Text = "Stopped."
})
$form.Controls.Add($stopBtn)

$form.Add_FormClosing({
    $timer.Stop()
    [HidVibTester]::Close()
})

[void]$form.ShowDialog()

