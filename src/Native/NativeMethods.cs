using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Twin2XInput.Native
{
    internal static class NativeMethods
    {
        public const uint GenericReadWrite = 0xC0000000u;
        public const uint FileShareReadWrite = 3u;
        public const uint OpenExisting = 3u;
        public const uint FileFlagOverlapped = 0x40000000u;
        public const uint DigcfPresentInterface = 0x12u;
        public const int ErrorIoPending = 997;
        public const uint WaitObject0 = 0u;
        public const uint VigemErrorNone = 0x20000000u;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XUSB_REPORT
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void VigemX360Notification(
            IntPtr client,
            IntPtr target,
            byte largeMotor,
            byte smallMotor,
            byte ledNumber,
            IntPtr userData);

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle deviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsageValue(
            int reportType,
            ushort usagePage,
            ushort linkCollection,
            ushort usage,
            out uint usageValue,
            IntPtr preparsedData,
            byte[] report,
            uint reportLength);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsages(
            int reportType,
            ushort usagePage,
            ushort linkCollection,
            [Out] ushort[] usageList,
            ref uint usageLength,
            IntPtr preparsedData,
            byte[] report,
            uint reportLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetOutputReport(SafeFileHandle deviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            out int requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            SafeFileHandle file,
            byte[] buffer,
            uint bytesToRead,
            out uint bytesRead,
            ref NativeOverlapped overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(
            SafeFileHandle file,
            ref NativeOverlapped overlapped,
            out uint bytesTransferred,
            bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(
            SafeFileHandle file,
            byte[] buffer,
            uint bytesToWrite,
            out uint bytesWritten,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEvent(
            IntPtr eventAttributes,
            bool manualReset,
            bool initialState,
            string name);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr vigem_alloc();

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void vigem_free(IntPtr vigem);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_connect(IntPtr vigem);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void vigem_disconnect(IntPtr vigem);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr vigem_target_x360_alloc();

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void vigem_target_free(IntPtr target);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_target_add(IntPtr vigem, IntPtr target);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_target_remove(IntPtr vigem, IntPtr target);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_target_x360_update(IntPtr vigem, IntPtr target, XUSB_REPORT report);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_target_x360_register_notification(
            IntPtr vigem,
            IntPtr target,
            VigemX360Notification callback,
            IntPtr userData);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void vigem_target_x360_unregister_notification(IntPtr target);

        [DllImport("vigemclient.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint vigem_target_x360_get_user_index(IntPtr vigem, IntPtr target, out uint index);
    }
}
