using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Twin2XInput.Native;

namespace Twin2XInput.Hid
{
    internal sealed class TwinHidPaths
    {
        public string Controller1Path;
        public string Controller2Path;
    }

    internal static class HidDeviceDiscovery
    {
        public static TwinHidPaths FindMatchingPaths(string deviceMatchToken)
        {
            var result = new TwinHidPaths();
            var fallbackPaths = new List<string>();

            Guid hidGuid;
            NativeMethods.HidD_GetHidGuid(out hidGuid);
            IntPtr deviceSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid, null, IntPtr.Zero, NativeMethods.DigcfPresentInterface);

            if (deviceSet == IntPtr.Zero || deviceSet == new IntPtr(-1))
                return result;

            try
            {
                var interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA))
                };

                for (uint i = 0; NativeMethods.SetupDiEnumDeviceInterfaces(
                    deviceSet, IntPtr.Zero, ref hidGuid, i, ref interfaceData); i++)
                {
                    string path = ReadInterfacePath(deviceSet, ref interfaceData);
                    if (path == null || path.IndexOf(deviceMatchToken, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (path.IndexOf("col01", StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Controller1Path = path;
                    else if (path.IndexOf("col02", StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Controller2Path = path;
                    else
                        fallbackPaths.Add(path);
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceSet);
            }

            AssignFallbackPaths(result, fallbackPaths);
            return result;
        }

        private static void AssignFallbackPaths(TwinHidPaths result, List<string> fallbackPaths)
        {
            if (result.Controller1Path == null && fallbackPaths.Count > 0)
                result.Controller1Path = fallbackPaths[0];
            if (result.Controller2Path == null && fallbackPaths.Count > 1)
                result.Controller2Path = fallbackPaths[1];
        }

        private static string ReadInterfacePath(IntPtr deviceSet, ref NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData)
        {
            int requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(
                deviceSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

            IntPtr buffer = Marshal.AllocHGlobal(Math.Max(requiredSize, 1024));
            try
            {
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                bool ok = NativeMethods.SetupDiGetDeviceInterfaceDetail(
                    deviceSet, ref interfaceData, buffer, requiredSize, out requiredSize, IntPtr.Zero);
                return ok ? Marshal.PtrToStringUni(IntPtr.Add(buffer, 4)) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
