using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;
using LiteMonitor.src.Core;

namespace LiteMonitor.src.SystemServices
{
    /// <summary>
    /// 电池服务：专门处理电池状态识别、数值计算及模拟测试逻辑
    /// </summary>
    public static class BatteryService
    {
        /// <summary>
        /// 获取电池相关数值
        /// </summary>
        public static float? GetBatteryValue(string key, Dictionary<string, ISensor> sensorCache)
        {
            // 1. 模拟模式逻辑 (用于 UI 测试)
            bool simulateBattery = false; // 默认关闭，可根据需要开启
            if (simulateBattery)
            {
                return GetSimulatedValue(key);
            }

            // 2. 真实硬件逻辑
            if (sensorCache.TryGetValue(key, out var sensor) && sensor.Value.HasValue)
            {
                float val = sensor.Value.Value;

                // 符号修正：充电时功耗/电流应为正号，放电时为负号
                if (key == "BAT.Power" || key == "BAT.Current")
                {
                    var powerStatus = MetricUtils.GetPowerStatus();
                    if (powerStatus.AcOnline)
                    {
                        // 如果接通电源但系统报告"未在充电" (Bypass模式)，强制归零
                        if (!powerStatus.IsCharging)
                        {
                            val = 0f;
                        }
                        else
                        {
                            if (val < 0) val = -val;
                        }
                    }
                    else
                    {
                        if (val > 0) val = -val;
                    }
                }
                return val;
            }

            return null;
        }

        private static float? GetSimulatedValue(string key)
        {
            var now = DateTime.Now;
            int sec = now.Second;

            // 前 30 秒：模拟 [高负载放电]，后 30 秒：模拟 [快充]
            bool isCharging = sec >= 30;
            
            float voltage = isCharging 
                ? 15.5f + ((sec - 30) * 0.05f) 
                : 16.8f - (sec * 0.06f);

            float power = isCharging ? -65.0f - (sec % 5) * 4.0f : 25.0f + (sec % 3) * 5.0f;
            float current = power / voltage;
            float percent = isCharging ? (sec - 30) * (100.0f / 30.0f) : 100.0f - (sec * (100.0f / 30.0f));

            return key switch
            {
                "BAT.Percent" => Math.Clamp(percent, 0f, 100f),
                "BAT.Power" => power,
                "BAT.Voltage" => voltage,
                "BAT.Current" => current,
                _ => null
            };
        }
    }
}
