using System;
using System.Linq;

namespace LiteMonitor.src.Core
{
    public enum MetricType
    {
        Percent,    // Load, BatPercent
        Temperature,// Temp
        Memory,     // Mem, Vram
        DataSize,   // Data Total
        DataSpeed,  // Net, Disk
        Frequency,  // Clock
        Power,      // Power
        Voltage,    // Voltage
        Current,    // Current
        RPM,        // Fan, Pump
        FPS,        // FPS
        Unknown
    }

    /// <summary>
    /// LiteMonitor 核心指标处理工具
    /// 包含：类型解析、数据格式化、阈值评估、状态管理
    /// </summary>
    public static class MetricUtils
    {
        // =========================================================
        // 1. 全局状态
        // =========================================================
        public static bool IsBatteryCharging = false;

        public const int STATE_SAFE = 0;
        public const int STATE_WARN = 1;
        public const int STATE_CRIT = 2;

        // =========================================================
        // 2. 类型解析 (原 MetricHelper)
        // =========================================================
        public static MetricType GetType(string key)
        {
            if (string.IsNullOrEmpty(key)) return MetricType.Unknown;
            
            if (key.Equals("FPS", StringComparison.OrdinalIgnoreCase)) return MetricType.FPS;
            if (key.StartsWith("BAT", StringComparison.OrdinalIgnoreCase))
            {
                if (key.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Percent;
                if (key.IndexOf("Voltage", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Voltage;
                if (key.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Current;
                if (key.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Power;
                return MetricType.Percent;
            }
            if (key.IndexOf("MEM", StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("VRAM", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Memory;

            if (key.IndexOf("LOAD", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Percent;
            if (key.IndexOf("TEMP", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Temperature;
            if (key.IndexOf("CLOCK", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Frequency;
            if (key.IndexOf("POWER", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Power;
            if (key.IndexOf("VOLTAGE", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.Voltage;
            if (key.IndexOf("FAN", StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("PUMP", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.RPM;
            if (key.StartsWith("NET", StringComparison.OrdinalIgnoreCase) || 
                key.StartsWith("DISK", StringComparison.OrdinalIgnoreCase)) return MetricType.DataSpeed;
            if (key.IndexOf("DATA", StringComparison.OrdinalIgnoreCase) >= 0) return MetricType.DataSize;
            
            return MetricType.Unknown;
        }

        // =========================================================
        // 3. 格式化逻辑 (原 Formatter)
        // =========================================================
        public static (string valStr, string unitStr) FormatValueParts(string key, float? raw)
        {
            float v = raw ?? 0.0f;
            var type = GetType(key);

            // 内存特殊处理
            if (type == MetricType.Memory)
            {
                 var cfg = Settings.Load();
                 if (cfg.MemoryDisplayMode == 1) // 容量模式
                 {
                     double totalGB = (key.IndexOf("MEM", StringComparison.OrdinalIgnoreCase) >= 0) 
                        ? Settings.DetectedRamTotalGB 
                        : Settings.DetectedGpuVramTotalGB;

                     if (totalGB > 0)
                         return FormatDataSizeParts((v / 100.0) * totalGB * 1073741824.0, 1);
                 }
                 return ($"{v:0.0}", "%");
            }

            if (type == MetricType.DataSpeed || type == MetricType.DataSize)
                return FormatDataSizeParts(v, 1);

            // 使用 GetDefaultUnit 统一获取单位 
            // 注意：FormatValueParts 主要用于 Panel 绘制，所以这里使用 Panel 上下文
            // 之前的硬编码：FPS=" FPS", RPM=" RPM", Temp="°C" (无空格)
            // 现在的 GetDefaultUnit(Panel)：FPS=" FPS", RPM=" RPM", Temp=" °C" (有空格)
            
            string suffix = (key.StartsWith("BAT", StringComparison.OrdinalIgnoreCase) && IsBatteryCharging) ? "⚡" : "";

            // 既然已经有统一的 GetDefaultUnit，我们应该尽量复用它。
            // 但需要注意 FormatValueParts 的返回值被用于 DrawString，
            // 如果这里的单位字符串改变了（例如 Temp 多了个空格），可能会微调 UI 显示。
            // 鉴于用户要求“统一封装解决”，我们将尝试复用。
            
            string unit = GetDefaultUnit(key, UnitContext.Panel);

            // 修正：Temp 在 Panel 模式下 GetDefaultUnit 返回 " °C"，但原 FormatValueParts 返回 "°C"。
            // 为了保持视觉一致性，或者我们认为 " °C" 才是新的标准？
            // 考虑到 HorizontalLayout 中已经统一使用了 " °C"，这里也应该统一。
            // 唯一的例外是 Power/Volt/Current 等，原代码是直接拼接，GetDefaultUnit 返回的也是无空格的。

            return type switch
            {
                MetricType.Frequency   => ($"{v/1000f:F1}", unit),
                MetricType.FPS         => ($"{v:0}", unit),
                MetricType.RPM         => ($"{v:0}", unit),
                // Temp 特殊处理：如果 GetDefaultUnit 返回带空格，这里也带空格，统一 UI。
                MetricType.Temperature => ($"{v:0.0}", unit), // 暂时保持原样 "°C"，避免面板数字和单位间距过大
                MetricType.Percent     => ($"{v:0.0}", unit + suffix),
                MetricType.Voltage     => ($"{v:F2}", unit + suffix),
                MetricType.Current     => ($"{v:F2}", unit + suffix),
                MetricType.Power       => (key.StartsWith("BAT") ? $"{v:F1}" : $"{v:F1}", unit + suffix),
                _                      => ($"{v:0.0}", "")
            };
        }

        public enum UnitContext
        {
            Panel,      // 主界面
            Taskbar,    // 任务栏
            Settings    // 设置界面 (显示默认值用)
        }

        public static string GetDefaultUnit(string key, UnitContext context)
        {
            var type = GetType(key);

            // 1. 设置界面需要看到 {u} 占位符，明确告知用户这是数据类单位
            if (context == UnitContext.Settings)
            {
                if (type == MetricType.Memory) return Settings.Load().MemoryDisplayMode == 1 ? "GB" : "%"; // 内存设置界面直接显示 GB/%，不显示 {u}，因为这个由全局设置控制
                if (type == MetricType.DataSize) return "{u}";
                if (type == MetricType.DataSpeed) return "{u}/s";
            }

            // 2. 实际显示逻辑 (Panel / Taskbar)
            // 内存特殊处理
            if (type == MetricType.Memory) return Settings.Load().MemoryDisplayMode == 1 ? "GB" : "%";
            
            // 数据类：Panel 显示完整单位，Taskbar 简写
            if (type == MetricType.DataSize) return "{u}"; // 实际上会被 FormatValueParts 替换为 KB/MB/GB
            if (type == MetricType.DataSpeed) return context == UnitContext.Taskbar ? "{u}" : "{u}/s";

            return type switch
            {
                MetricType.Percent     => "%",
                MetricType.Temperature => "°C",
                MetricType.Frequency   => "GHz",
                MetricType.Power       => "W",
                MetricType.Voltage     => "V",
                MetricType.Current     => "A",
                MetricType.FPS         => context == UnitContext.Taskbar ? "F" : " FPS",
                MetricType.RPM         => context == UnitContext.Taskbar ? "R" : " RPM",
                _ => ""
            };
        }

        // 兼容旧重载 (逐步废弃)
        public static string GetDefaultUnit(string key, bool isTaskbarMode)
        {
            return GetDefaultUnit(key, isTaskbarMode ? UnitContext.Taskbar : UnitContext.Panel);
        }

        public static string GetDisplayUnit(string key, string calculatedUnit, string userFormat)
        {
            if (string.IsNullOrEmpty(userFormat) || userFormat.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(userFormat) && userFormat != null) return ""; // Empty = Hide
                return (GetType(key) == MetricType.DataSpeed) ? calculatedUnit + "/s" : calculatedUnit;
            }
            return userFormat.Contains("{u}") ? userFormat.Replace("{u}", calculatedUnit) : userFormat;
        }
        // [Refactor] 统一数据大小格式化逻辑
        
        /// <summary>
        /// 将字节数转换为带单位的友好显示，返回数值字符串与单位字符串。
        /// </summary>
        /// <param name="bytes">原始字节数，允许为负值，内部会按绝对值处理。</param>
        /// <param name="decimals">
        /// 保留小数位数：
        /// -1 表示自动：KB/MB 保留 1 位，GB 及以上保留 2 位；
        /// 0  表示整数；
        /// >0 表示固定小数位。
        /// </param>
        /// <returns>
        /// 元组：(数值字符串, 单位字符串)，例如 ("12.3", "MB")。
        /// </returns>
        public static (string val, string unit) FormatDataSizeParts(double bytes, int decimals = -1)
        {
            // 单位列表，从 KB 开始，后续依次乘以 1024 得到 MB、GB、TB、PB
            string[] sizes = { "KB", "MB", "GB", "TB", "PB" };

            // 将字节转换为 KB 作为基准
            double len = bytes / 1024.0;

            // 逐级放大单位，直到数值小于 1024 或到达最大单位
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024.0;
            }

            // 根据 order 和 decimals 参数决定最终格式字符串
            string format = decimals switch
            {
                // 自动模式：KB/MB 用 1 位小数，GB 及以上用 2 位小数
                < 0 => order <= 1 ? "0.0" : "0.00",
                // 整数模式
                0   => "0",
                // 固定小数位模式，拼接出如 "0.000" 的格式
                _   => "0." + new string('0', decimals)
            };

            // 返回格式化后的数值与对应单位
            return (len.ToString(format), sizes[order]);
        }

        public static string FormatDataSize(double bytes, string suffix = "", int decimals = -1)
        {
            var (val, unit) = FormatDataSizeParts(bytes, decimals);
            return $"{val}{unit}{suffix}";
        }

        public static string FormatHorizontalValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            
            // [Optimization] Avoid chained Replace calls which cause multiple allocations.
            // Units are almost always at the end of the string.
            string clean = value;

            if (clean.EndsWith("/s", StringComparison.Ordinal))
            {
                clean = clean.Substring(0, clean.Length - 2);
            }
            
            if (clean.EndsWith("RPM", StringComparison.Ordinal))
            {
                clean = clean.Substring(0, clean.Length - 3) + "R";
            }
            else if (clean.EndsWith("FPS", StringComparison.Ordinal))
            {
                clean = clean.Substring(0, clean.Length - 3) + "F";
            }

            clean = clean.Trim();

            int splitIndex = -1;
            for (int i = 0; i < clean.Length; i++)
            {
                if (!char.IsDigit(clean[i]) && clean[i] != '.' && clean[i] != '-') { splitIndex = i; break; }
            }

            if (splitIndex <= 0) return clean;

            string numStr = clean.Substring(0, splitIndex);
            string unit = clean.Substring(splitIndex).Trim();

            // [Refactor] 统一数值格式化
            if (double.TryParse(numStr, out double num))
                // 数值 >= 100 时，四舍五入为整数
                return (num >= 100 ? ((int)Math.Round(num)).ToString() : numStr) + unit;
            
            return clean;
        }

        // =========================================================
        // 4. 评估逻辑 (原 MetricEvaluator)
        // =========================================================
        public static int GetState(string key, double value)
        {
            if (double.IsNaN(value)) return STATE_SAFE;
            if (key.StartsWith("BAT", StringComparison.OrdinalIgnoreCase) && value < 0) return STATE_SAFE;

            var type = GetType(key);
            double checkValue = value;

            // 自适应缩放
            if (type is MetricType.Frequency or MetricType.Power or MetricType.RPM or MetricType.FPS or MetricType.Voltage or MetricType.Current)
            {
                 checkValue = GetUnifiedPercent(key, value) * 100.0;
            }
            else if (type is MetricType.DataSpeed or MetricType.DataSize)
            {
                checkValue = value / 1048576.0; // To MB
            }

            var (warn, crit) = GetThresholds(key);

            // 电池低电量反向判断
            if (key.Equals("BAT.Percent", StringComparison.OrdinalIgnoreCase))
            {
                if (checkValue <= crit) return STATE_CRIT;
                if (checkValue <= warn) return STATE_WARN;
                return STATE_SAFE;
            }

            if (checkValue >= crit) return STATE_CRIT;
            if (checkValue >= warn) return STATE_WARN;

            return STATE_SAFE;
        }

        public static (double warn, double crit) GetThresholds(string key)
        {
            var cfg = Settings.Load();
            var th = cfg.Thresholds;
            var type = GetType(key);

            return type switch
            {
                MetricType.Temperature => (th.Temp.Warn, th.Temp.Crit),
                MetricType.DataSpeed => key.StartsWith("NET") 
                    ? (key.IndexOf("UP", StringComparison.OrdinalIgnoreCase) >= 0 ? (th.NetUpMB.Warn, th.NetUpMB.Crit) : (th.NetDownMB.Warn, th.NetDownMB.Crit))
                    : (th.DiskIOMB.Warn, th.DiskIOMB.Crit),
                MetricType.DataSize => (key.IndexOf("UP", StringComparison.OrdinalIgnoreCase) >= 0 ? (th.DataUpMB.Warn, th.DataUpMB.Crit) : (th.DataDownMB.Warn, th.DataDownMB.Crit)),
                MetricType.Percent => key.Equals("BAT.Percent", StringComparison.OrdinalIgnoreCase) ? (60, 20) : (th.Load.Warn, th.Load.Crit),
                _ => (th.Load.Warn, th.Load.Crit)
            };
        }

        public static double GetUnifiedPercent(string key, double value)
        {
            var type = GetType(key);
            if (type is MetricType.Frequency or MetricType.Power or MetricType.RPM or MetricType.FPS or MetricType.Voltage or MetricType.Current)
                return GetAdaptivePercentage(key, value);
            
            return value / 100.0;
        }

        public static double GetAdaptivePercentage(string key, double val)
        {
            var cfg = Settings.Load();
            float max = 1.0f;

            if (key == "CPU.Clock") max = cfg.RecordedMaxCpuClock;
            else if (key == "CPU.Power") max = cfg.RecordedMaxCpuPower;
            else if (key == "GPU.Clock") max = cfg.RecordedMaxGpuClock;
            else if (key == "GPU.Power") max = cfg.RecordedMaxGpuPower;
            else if (key == "CPU.Fan") max = cfg.RecordedMaxCpuFan;
            else if (key == "CPU.Pump") max = cfg.RecordedMaxCpuPump;
            else if (key == "CASE.Fan") max = cfg.RecordedMaxChassisFan;
            else if (key == "GPU.Fan") max = cfg.RecordedMaxGpuFan;
            else if (key == "FPS") max = cfg.RecordedMaxFps;
            else if (key == "CPU.Voltage") max = 1.6f;
            else if (key == "BAT.Power") max = 100f;
            else if (key == "BAT.Voltage") max = 20f;
            else if (key == "BAT.Current") max = 5f;

            if (max < 1) max = 1;
            double pct = val / max;
            return pct > 1.0 ? 1.0 : pct;
        }
        
        // =========================================================
        // 5. 基础转换
        // =========================================================
        public static int ParseInt(string s) => int.TryParse(new string(s?.Where(c => char.IsDigit(c) || c == '-').ToArray()), out int v) ? v : 0;
        public static double ParseDouble(string s) => double.TryParse(new string(s?.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray()), out double v) ? v : 0;
        public static string ToStr(double v, string format = "F1") => v.ToString(format);
    }
}
