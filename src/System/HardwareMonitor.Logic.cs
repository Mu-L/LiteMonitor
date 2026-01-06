using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using LibreHardwareMonitor.Hardware;
using LiteMonitor.src.Core;
using Debug = System.Diagnostics.Debug;
namespace LiteMonitor.src.SystemServices
{
    // 依然是 HardwareMonitor 的一部分
    public sealed partial class HardwareMonitor
    {
        // ===========================================================
        // ===================== 公共取值入口 =========================
        // ===========================================================
        public float? Get(string key)
        {
            EnsureMapFresh();
            // ★★★ [新增] 拦截 CPU.Load 请求 ★★★
            // ★★★ 核心修复：手动计算所有核心的平均值，不再信那个 0.8% 的 Total ★★★
            if (key == "CPU.Load")
            {
                // -------------------------------------------------------
                // 1. [第一优先级] 系统计数器模式
                // -------------------------------------------------------
                // 如果用户在设置里勾选了"Use System Performance Counter"，
                // 则直接返回系统计数器的值 (Processor Utility / Time)，
                // 跳过后面所有的 LHM 传感器逻辑。
                if (_cfg.UseSystemCpuLoad) 
                {
                    return _lastSystemCpuLoad;
                }

                // -------------------------------------------------------
                // 2. [第二优先级] 手动聚合核心负载 (解决 0.8% 问题)
                // -------------------------------------------------------
                var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                if (cpu != null)
                {
                    double totalLoad = 0;
                    int coreCount = 0;

                    foreach (var s in cpu.Sensors)
                    {
                        // 只看 Load 类型
                        if (s.SensorType != SensorType.Load) continue;

                        string name = s.Name;

                        // ★★★ 严格过滤策略 ★★★
                        // 1. 白名单：必须包含 "Core" 且包含 "#" (如 "CPU Core #1")
                        //    这能 100% 排除 "Core Max", "Core Average" 等导致虚高的统计值。
                        // 2. 黑名单：再次显式排除 Total/SOC/Max/Average 以防万一。
                        bool isRealCore = Has(name, "Core") && Has(name, "#");
                        
                        bool isNotStat  = !Has(name, "Total") && 
                                        !Has(name, "SOC") && 
                                        !Has(name, "Max") && 
                                        !Has(name, "Average");

                        if (isRealCore && isNotStat)
                        {
                            if (s.Value.HasValue)
                            {
                                totalLoad += s.Value.Value;
                                coreCount++;
                            }
                        }
                    }

                    // 只要找到了有效的带编号核心，就返回平均值
                    if (coreCount > 0)
                    {
                        return (float)(totalLoad / coreCount);
                    }
                }

                // -------------------------------------------------------
                // 3. [第三优先级] 兜底策略
                // -------------------------------------------------------
                // 如果代码走到这，说明：
                // A. 没开系统计数器
                // B. 也没找到任何带 "#" 的核心 (可能是极罕见的单核无编号CPU)
                // 此时只能读取默认映射的 Total 传感器。
                lock (_lock)
                {
                    if (_map.TryGetValue("CPU.Load", out var s) && s.Value.HasValue)
                        return s.Value.Value;
                }
                
                return 0f;
            }

            // ★★★ [终极修复] CPU.Temp 智能取最大值 ★★★
            // ==========================================
            if (key == "CPU.Temp")
            {
                float maxTemp = -1000f;
                bool found = false;

                var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                if (cpu != null)
                {
                    foreach (var s in cpu.Sensors)
                    {
                        // 1. 基础门槛：必须是温度，必须有正数值
                        if (s.SensorType != SensorType.Temperature) continue;
                        if (!s.Value.HasValue || s.Value.Value <= 0) continue;

                        string name = s.Name;

                        // 2. 【黑名单过滤】
                        // 只要名字里带这些词，统统不要
                        if (Has(name, "Distance")) continue; // 排除距离TjMax
                        if (Has(name, "Average"))  continue; // 排除平均值
                        if (Has(name, "Max"))      continue; // 排除 LHM 算好的 Max (既然我们要自己算)

                        // 3. 【幸存者PK】
                        // 此时剩下的只剩下：
                        // - CPU Package
                        // - CPU Core #1, Core #2 ...
                        // - CPU P-Core #1, E-Core #1 ... (大小核都能进来)
                        // - CPU CCD1 (AMD 也能进来)
                        
                        // 我们在这些“真·物理温度”里取一个最高的
                        if (s.Value.Value > maxTemp)
                        {
                            maxTemp = s.Value.Value;
                            found = true;
                        }
                    }
                }

                if (found) return maxTemp;

                // 4. 字典兜底 (兼容主板)
                lock (_lock)
                {
                    if (_map.TryGetValue("CPU.Temp", out var s) && s.Value.HasValue)
                        return s.Value.Value;
                }
                return 0f;
            }

            // 1. 网络与磁盘 (独立逻辑)
            switch (key)
            {
                case "NET.Up": case "NET.Down": return GetNetworkValue(key);
                case "DISK.Read": case "DISK.Write": return GetDiskValue(key);
            }

            // ★★★ [新增] 获取今日流量 (从 TrafficLogger 拿) ★★★
            if (key == "DATA.DayUp")
            {
                return TrafficLogger.GetTodayStats().up;
            }
            if (key == "DATA.DayDown")
            {
                return TrafficLogger.GetTodayStats().down;
            }

            // 2. 频率与功耗 (复合计算逻辑)
            if (key.Contains("Clock") || key.Contains("Power"))
            {
                return GetHardwareCompositeValue(key);
            }

            // ★ 修改：增强的内存计算逻辑
            if (key == "MEM.Load")
            {
                // 只要还没探测到，就尝试探测
                if (Settings.DetectedRamTotalGB <= 0)
                {
                    lock (_lock)
                    {
                        if (_map.TryGetValue("MEM.Used", out var u) && _map.TryGetValue("MEM.Available", out var a))
                        {
                            if (u.Value.HasValue && a.Value.HasValue)
                            {
                                
                                float rawTotal = u.Value.Value + a.Value.Value;
                                // 如果数值 > 512，认为是 MB，除以 1024；否则认为是 GB (Data 类型通常直接是 GB)
                                Settings.DetectedRamTotalGB = rawTotal > 512.0f ? rawTotal / 1024.0f : rawTotal;
                            }
                        }
                    }
                }
            }

            // 3. 显存百分比 (特殊计算)
            if (key == "GPU.VRAM")
            {
                float? used = Get("GPU.VRAM.Used");
                float? total = Get("GPU.VRAM.Total");
                if (used.HasValue && total.HasValue && total > 0)
                {
                    // 假设 total 是 MB (LHM SmallData 默认是 MB)这里统一转成 GB 存起来
                    if (Settings.DetectedGpuVramTotalGB <= 0)
                    {
                        Settings.DetectedGpuVramTotalGB = total.Value / 1024f;
                    }

                    // 简单单位换算防止数值过大溢出 (虽 float 够用，但为了逻辑统一)
                    if (total > 10485760) { used /= 1048576f; total /= 1048576f; }
                    return used / total * 100f;
                }
                // Fallback: 如果有 Load 传感器直接用
                lock (_lock) { if (_map.TryGetValue("GPU.VRAM.Load", out var s) && s.Value.HasValue) return s.Value; }
                return null;
            }

            // 4. 普通传感器 (直接读字典)
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var sensor))
                {
                    var val = sensor.Value;
                    if (val.HasValue && !float.IsNaN(val.Value))
                    {
                        _lastValid[key] = val.Value;
                        return val.Value;
                    }
                    if (_lastValid.TryGetValue(key, out var last)) return last;
                }
            }

            return null;
        }

        // ===========================================================
        // ========= [核心算法] CPU/GPU 频率功耗复合计算 ==============
        // ===========================================================
        private float? GetHardwareCompositeValue(string key)
        {
            // --- CPU 频率：加权平均算法 ---
            if (key == "CPU.Clock")
            {
                if (_cpuCoreCache.Count == 0) return null;

                double sum = 0;
                int count = 0;
                float maxRaw = 0;

                // Zen 5 修正 (保持不动)
                float correctionFactor = 1.0f;
                if (_cpuBusSpeedSensor != null && _cpuBusSpeedSensor.Value.HasValue)
                {
                    float bus = _cpuBusSpeedSensor.Value.Value;
                    if (bus > 1.0f && bus < 20.0f) 
                    {
                        float factor = 100.0f / bus;
                        if (factor > 2.0f && factor < 10.0f) correctionFactor = factor;
                    }
                }

                foreach (var core in _cpuCoreCache)
                {
                    if (core.Clock == null || !core.Clock.Value.HasValue) continue;
                    float clk = core.Clock.Value.Value * correctionFactor;

                    // 记录最大值 (给配置记录用，不参与显示计算)
                    if (clk > maxRaw) maxRaw = clk;

                    // ★★★ 核心逻辑：只过滤明显错误的极低值 ★★★
                    // 只要大于 400MHz (0.4G)，就认为是有效核心。
                    // 重点：不要过滤 E-Core！不要过滤待机核心！
                    // 哪怕它是 800MHz 或 2400MHz，都要算进去，这样才能把 5.0G 拉低到 3.x G。
                    // 同时排除 100MHz 的总线干扰。
                    if (clk > 400f) 
                    {
                        sum += clk;
                        count++;
                    }
                }
                
                // 更新最大值记录
                if (maxRaw > 0) _cfg.UpdateMaxRecord(key, maxRaw);

                // ★★★ 简单平均值 ★★★
                // (P核 4.8 + P核 4.8 ... + E核 2.4 + E核 2.4) / 总数
                // 结果就是稳定的 3.x GHz，完全对齐任务管理器。
                if (count > 0)
                {
                    return (float)(sum / count);
                }

                return maxRaw;
            }

            // --- CPU 功耗：直接读取或回落 ---
            if (key == "CPU.Power")
            {
                // 优先从 Map 读 (NormalizeKey 已处理 Package 映射)
                lock (_lock)
                {
                    if (_map.TryGetValue("CPU.Power", out var s) && s.Value.HasValue)
                    {
                        _cfg.UpdateMaxRecord(key, s.Value.Value);
                        return s.Value.Value;
                    }
                }
                return null;
            }

            // --- GPU 频率/功耗：使用显卡缓存 ---
            if (key.StartsWith("GPU"))
            {
                if (_cachedGpu == null) return null;

                ISensor? s = null;
                // 注意：GPU 传感器少，LINQ 查询开销可忽略
                if (key == "GPU.Clock")
                { 
                    // 增加 "shader" 匹配
                    s = _cachedGpu.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Clock && 
                        (Has(x.Name, "graphics") || Has(x.Name, "core") || Has(x.Name, "shader")));
                    
                    // ★★★ 【修复 1】频率异常过滤 ★★★
                    // 如果读数为 0 (休眠) 是正常的，但如果超过 6000MHz (6GHz) 肯定是传感器抽风
                    if (s != null && s.Value.HasValue)
                    {
                        float val = s.Value.Value;
                        if (val > 6000.0f) return null; // 过滤异常高频
                        
                        _cfg.UpdateMaxRecord(key, val);
                        return val;
                    }
                }
                else if (key == "GPU.Power")
                {
                    // 增加 "core" 匹配
                    s = _cachedGpu.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Power && 
                        (Has(x.Name, "package") || Has(x.Name, "ppt") || Has(x.Name, "board") || Has(x.Name, "core") || Has(x.Name, "gpu")));

                    // ★★★ 【修复 2】功耗异常过滤 (你的问题核心) ★★★
                    // 消费级显卡瞬间功耗不可能超过 1500W (4090 峰值也就 600W 左右)
                    // 16368W 显然是错误数据，直接丢弃
                    if (s != null && s.Value.HasValue)
                    {
                        float val = s.Value.Value;
                        
                        // 设定一个 2000W 的安全阀值
                        if (val > 2000.0f) return null; 

                        _cfg.UpdateMaxRecord(key, val);
                        return val;
                    }
                }
            }

            return null;
        }

        // ===========================================================
        // ==================== 网络 (Network) =======================
        // ===========================================================
        private float? GetNetworkValue(string key)
        {
            // 1. 优先手动指定
            if (!string.IsNullOrWhiteSpace(_cfg.PreferredNetwork))
            {
                var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Network && h.Name.Equals(_cfg.PreferredNetwork, StringComparison.OrdinalIgnoreCase));
                if (hw != null) return ReadNetworkSensor(hw, key);
            }

            // 2. 自动选优 (带缓存)
            return GetBestNetworkValue(key);
        }

        private float? GetBestNetworkValue(string key)
        {
            // A. 尝试运行时缓存
            if (_cachedNetHw != null)
            {
                // ★★★ 【修复 1】存活检查：如果缓存的硬件对象已经不在当前的硬件列表中（已失效），强制丢弃 ★★★
                if (!_computer.Hardware.Contains(_cachedNetHw))
                {
                    _cachedNetHw = null;
                }
                else
                {
                    float? cachedVal = ReadNetworkSensor(_cachedNetHw, key);
                    // 逻辑优化：
                    // 1. 如果有流量，直接用。
                    // 2. 如果没流量，但距离上次全盘扫描还不到 3 秒，也直接用。
                    if ((cachedVal.HasValue && cachedVal.Value > 0.1f) ||
                        (DateTime.Now - _lastNetScan).TotalSeconds < 3)
                    {
                        return cachedVal;
                    }
                }
                // 如果超过 3 秒还是没流量，说明可能切网卡了，放行到底部去全盘扫描
            }

            // ★★★ [漏掉的部分] B. 尝试启动时缓存 (Settings 中的记录) ★★★
            // 确保 Settings.cs 里已经定义了 public string LastAutoNetwork { get; set; } = "";
            if (_cachedNetHw == null && !string.IsNullOrEmpty(_cfg.LastAutoNetwork))
            {
                // 尝试直接找上次记住的网卡
                var savedHw = _computer.Hardware.FirstOrDefault(h => h.Name == _cfg.LastAutoNetwork);
                if (savedHw != null)
                {
                    // 找到了！直接设为缓存，跳过全盘扫描
                    _cachedNetHw = savedHw;
                    _lastNetScan = DateTime.Now;
                    return ReadNetworkSensor(savedHw, key);
                }
            }

            // C. 全盘扫描 (代码保持不变)
            IHardware? bestHw = null;
            double bestScore = double.MinValue;
            ISensor? bestTarget = null;

            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Network))
            {
                // ... (你的原有扫描逻辑) ...
                // ... (复制你文件里 foreach 的内容) ...
                double penalty = _virtualNicKW.Any(k => Has(hw.Name, k)) ? -1e9 : 0;
                ISensor? up = null, down = null;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != SensorType.Throughput) continue;
                    if (_upKW.Any(k => Has(s.Name, k))) up ??= s;
                    if (_downKW.Any(k => Has(s.Name, k))) down ??= s;
                }
                if (up == null && down == null) continue;
                double score = (up?.Value ?? 0) + (down?.Value ?? 0) + penalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHw = hw;
                    bestTarget = (key == "NET.Up") ? up : down;
                }
            }

            // D. 更新缓存
            if (bestHw != null)
            {
                _cachedNetHw = bestHw;
                _lastNetScan = DateTime.Now;
                
                // ★★★ [漏掉的部分] 记住这次的选择 ★★★
                if (_cfg.LastAutoNetwork != bestHw.Name)
                {
                    _cfg.LastAutoNetwork = bestHw.Name;
                }
            }

            // ... (返回结果部分保持不变) ...
            if (bestTarget?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private float? ReadNetworkSensor(IHardware hw, string key)
        {
            ISensor? target = null;
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType != SensorType.Throughput) continue;
                if (key == "NET.Up" && _upKW.Any(k => Has(s.Name, k))) { target = s; break; } // 找到即停
                if (key == "NET.Down" && _downKW.Any(k => Has(s.Name, k))) { target = s; break; }
            }

            if (target?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private static readonly string[] _upKW = { "upload", "up", "sent", "send", "tx", "transmit" };
        private static readonly string[] _downKW = { "download", "down", "received", "receive", "rx" };
        private static readonly string[] _virtualNicKW = { "virtual", "vmware", "hyper-v", "hyper v", "vbox", "loopback", "tunnel", "tap", "tun", "bluetooth", "zerotier", "tailscale", "wan miniport" };

        // ===========================================================
        // ===================== 磁盘 (Disk) =========================
        // ===========================================================
        private float? GetDiskValue(string key)
        {
            if (!string.IsNullOrWhiteSpace(_cfg.PreferredDisk))
            {
                var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Storage && h.Name.Equals(_cfg.PreferredDisk, StringComparison.OrdinalIgnoreCase));
                if (hw != null) return ReadDiskSensor(hw, key);
            }
            return GetBestDiskValue(key);
        }

        private float? GetBestDiskValue(string key)
        {
           // A. 尝试运行时缓存
            if (_cachedDiskHw != null)
            {
                // ★★★ 【修复 2】存活检查：防止持有僵尸对象的引用 ★★★
                if (!_computer.Hardware.Contains(_cachedDiskHw))
                {
                    _cachedDiskHw = null;
                }
                else
                {
                    float? cachedVal = ReadDiskSensor(_cachedDiskHw, key);
                    // 有读写活动或冷却期内，直接返回
                    if ((cachedVal.HasValue && cachedVal.Value > 0.1f) || (DateTime.Now - _lastDiskScan).TotalSeconds < 10)
                        return cachedVal;
                }
            }

            // ★★★ [新增] B. 尝试启动时缓存 (Settings 记忆) ★★★
            if (_cachedDiskHw == null && !string.IsNullOrEmpty(_cfg.LastAutoDisk))
            {
                var savedHw = _computer.Hardware.FirstOrDefault(h => h.Name == _cfg.LastAutoDisk);
                if (savedHw != null)
                {
                    // 命中缓存！跳过全盘扫描
                    _cachedDiskHw = savedHw;
                    _lastDiskScan = DateTime.Now;
                    return ReadDiskSensor(savedHw, key);
                }
            }

            // C. 全盘扫描 (逻辑保持不变)
            string sysPrefix = "";
            try { sysPrefix = Path.GetPathRoot(Environment.SystemDirectory)?.Substring(0, 2) ?? ""; } catch { }

            IHardware? bestHw = null;
            double bestScore = double.MinValue;
            ISensor? bestTarget = null;

            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage))
            {
                // ... (复制你原有的扫描逻辑) ...
                bool isSystem = !string.IsNullOrEmpty(sysPrefix) && (Has(hw.Name, sysPrefix) || hw.Sensors.Any(s => Has(s.Name, sysPrefix)));
                
                ISensor? read = null, write = null;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != SensorType.Throughput) continue;
                    if (Has(s.Name, "read")) read ??= s;
                    if (Has(s.Name, "write")) write ??= s;
                }

                if (read == null && write == null) continue;

                double score = (read?.Value ?? 0) + (write?.Value ?? 0);
                if (isSystem) score += 1e9; // 系统盘优先

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHw = hw;
                    bestTarget = (key == "DISK.Read") ? read : write;
                }
            }

            // D. 更新缓存
            if (bestHw != null)
            {
                _cachedDiskHw = bestHw;
                _lastDiskScan = DateTime.Now;
                
                // ★★★ [新增] 记住这次的选择 ★★★
                if (_cfg.LastAutoDisk != bestHw.Name)
                {
                    _cfg.LastAutoDisk = bestHw.Name;
                }
            }

            // E. 返回结果
            if (bestTarget?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private float? ReadDiskSensor(IHardware hw, string key)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType != SensorType.Throughput) continue;
                if (key == "DISK.Read" && Has(s.Name, "read")) return SafeRead(s, key);
                if (key == "DISK.Write" && Has(s.Name, "write")) return SafeRead(s, key);
            }
            return SafeRead(null, key);
        }

        private float? SafeRead(ISensor? s, string key)
        {
            if (s?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        // ===========================================================
        // ================== 辅助 / 映射 (Helpers) ===================
        // ===========================================================
        
        // 静态工具：菜单使用
        public static List<string> ListAllNetworks() => Instance?._computer.Hardware.Where(h => h.HardwareType == HardwareType.Network).Select(h => h.Name).Distinct().ToList() ?? new List<string>();
        public static List<string> ListAllDisks() => Instance?._computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).Select(h => h.Name).Distinct().ToList() ?? new List<string>();

        // [重要] 传感器名称标准化映射
        private static string? NormalizeKey(IHardware hw, ISensor s)
        {
            string name = s.Name;
            var type = hw.HardwareType;

            // --- CPU ---
            if (type == HardwareType.Cpu)
            {
                // 新代码：增加 "package" 支持，防止某些 CPU 把总负载叫 "CPU Package"
                if (s.SensorType == SensorType.Load)
                {
                    if (Has(name, "total") || Has(name, "package")) 
                        return "CPU.Load";
                }
                // [深度优化后的温度匹配逻辑]
                if (s.SensorType == SensorType.Temperature)
                {
                    // 1. 黄金标准：包含这些词的通常就是我们要的
                    if (Has(name, "package") ||  // Intel/AMD 标准
                        Has(name, "average") ||  // LHM 聚合数据
                        Has(name, "tctl") ||     // AMD 风扇控制温度 (最准)
                        Has(name, "tdie") ||     // AMD 核心硅片温度
                        Has(name, "ccd") ||       // AMD 核心板
                        Has(name, "cores"))     // 通用核心温度
                    {
                        return "CPU.Temp";
                    }

                    // 2. 银牌标准：通用名称兜底 (修复 AMD 7840HS 等移动端 CPU)
                    // 必须严格排除干扰项 (如 SOC, VRM, Pump 等)
                    if ((Has(name, "cpu") || Has(name, "core")) && 
                        !Has(name, "soc") &&     // 排除核显/片上系统
                        !Has(name, "vrm") &&     // 排除供电
                        !Has(name, "fan") &&     // 排除风扇(虽类型不同，但防名字干扰)
                        !Has(name, "pump") &&    // 排除水泵
                        !Has(name, "liquid") &&  // 排除水冷液
                        !Has(name, "coolant") && // 排除冷却液
                        !Has(name, "distance"))  // 排除 "Distance to TjMax"
                    {
                        // 注意：这里可能会匹配到 "Core #1"，虽然不是 Package，
                        // 但在没有 Package 传感器的情况下，这是唯一的有效读数。
                        return "CPU.Temp";
                    }
                }
                if (s.SensorType == SensorType.Power && (Has(name, "package") || Has(name, "cores"))) return "CPU.Power";
                // 注意：Clock 不走 Map，走加权平均缓存，所以这里不需要映射
            }

            // --- GPU ---
            if (type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            {
                if (s.SensorType == SensorType.Load && (Has(name, "core") || Has(name, "d3d 3d"))) return "GPU.Load";
                if (s.SensorType == SensorType.Temperature && (Has(name, "core") || Has(name, "hot spot") || Has(name, "soc") || Has(name, "vr"))) return "GPU.Temp";
                
                // VRAM
                if (s.SensorType == SensorType.SmallData && (Has(name, "memory") || Has(name, "dedicated")))
                {
                    if (Has(name, "used")) return "GPU.VRAM.Used";
                    if (Has(name, "total")) return "GPU.VRAM.Total";
                }
                if (s.SensorType == SensorType.Load && Has(name, "memory")) return "GPU.VRAM.Load";
            }

            // --- Memory ---
            if (type == HardwareType.Memory) 
            {
                if (Has(hw.Name, "virtual")) return null;
                // 1. 负载 (保持不变)
                if (s.SensorType == SensorType.Load && Has(name, "memory")) return "MEM.Load";
                
                // 2. ★ 增强版匹配：同时接受 Data 和 SmallData
                if (s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData)
                {
                    if (Has(name, "used")) return "MEM.Used";
                    if (Has(name, "available")) return "MEM.Available";
                }
            }

            return null;
        }

        private static bool Has(string source, string sub)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sub)) return false;
            return source.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}