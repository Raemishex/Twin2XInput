using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Twin2XInput.Config
{
    internal sealed class BridgeSettings
    {
        public string TargetVid = "0810";
        public string TargetPid = "0001";
        public bool SwapRightAxes = true;
        public bool InvertLY = true;
        public bool InvertRY = true;
        public bool SwapBumpersTriggers = false;
        public bool EnableSecondPad = false;
        public bool MirrorVibToPad2 = false;
        public int DeadzonePct = 14;
        public int VibBoostPct = 140;

        private static string IniPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "twin2xinput.ini"); }
        }

        public string DeviceMatchToken
        {
            get
            {
                return string.IsNullOrEmpty(TargetPid)
                    ? "vid_" + TargetVid
                    : "vid_" + TargetVid + "&pid_" + TargetPid;
            }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(IniPath)) return;
                foreach (string rawLine in File.ReadAllLines(IniPath))
                {
                    ApplySettingLine(rawLine);
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(IniPath, new[]
                {
                    "TargetVid=" + TargetVid,
                    "TargetPid=" + TargetPid,
                    "SwapRightAxes=" + SwapRightAxes,
                    "InvertLY=" + InvertLY,
                    "InvertRY=" + InvertRY,
                    "SwapBumpersTriggers=" + SwapBumpersTriggers,
                    "EnableSecondPad=" + EnableSecondPad,
                    "MirrorVibToPad2=" + MirrorVibToPad2,
                    "DeadzonePct=" + DeadzonePct,
                    "VibBoostPct=" + VibBoostPct
                });
            }
            catch { }
        }

        private void ApplySettingLine(string rawLine)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) return;

            string[] parts = line.Split(new[] { '=' }, 2);
            if (parts.Length != 2) return;

            string key = parts[0].Trim();
            string val = parts[1].Trim();
            bool boolVal;
            int intVal;

            if (key == "TargetVid" && IsHexId(val)) TargetVid = val.ToLowerInvariant();
            else if (key == "TargetPid" && (val.Length == 0 || IsHexId(val))) TargetPid = val.ToLowerInvariant();
            else if (key == "SwapRightAxes" && bool.TryParse(val, out boolVal)) SwapRightAxes = boolVal;
            else if (key == "InvertLY" && bool.TryParse(val, out boolVal)) InvertLY = boolVal;
            else if (key == "InvertRY" && bool.TryParse(val, out boolVal)) InvertRY = boolVal;
            else if (key == "SwapBumpersTriggers" && bool.TryParse(val, out boolVal)) SwapBumpersTriggers = boolVal;
            else if (key == "EnableSecondPad" && bool.TryParse(val, out boolVal)) EnableSecondPad = boolVal;
            else if (key == "MirrorVibToPad2" && bool.TryParse(val, out boolVal)) MirrorVibToPad2 = boolVal;
            else if (key == "DeadzonePct" && int.TryParse(val, out intVal)) DeadzonePct = Clamp(intVal, 0, 45);
            else if (key == "VibBoostPct" && int.TryParse(val, out intVal)) VibBoostPct = Clamp(intVal, 100, 200);
        }

        private static bool IsHexId(string value)
        {
            return Regex.IsMatch(value, "^[0-9a-fA-F]{4}$");
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
