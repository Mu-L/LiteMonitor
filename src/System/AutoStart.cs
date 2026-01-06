using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

namespace LiteMonitor.src.SystemServices
{
    public static class AutoStart
    {
        private const string TaskName = "LiteMonitor_AutoStart";

        public static void Set(bool enabled)
        {
            string exePath = Process.GetCurrentProcess().MainModule!.FileName!;

            // 1. 网络路径拦截 (保留你的原始逻辑)
            try
            {
                string root = Path.GetPathRoot(exePath)!;
                if ((!string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Network) || new Uri(exePath).IsUnc)
                {
                    MessageBox.Show("Windows 计划任务不支持在网络路径下运行。\n请移动到本地硬盘。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch { }

            if (enabled)
            {
                // 使用 XML 方案，这是唯一能同时满足 [不报PowerShell错误] + [实现电池启动] 的方案
                try
                {
                    // 生成临时 XML 文件路径
                    string tempXmlPath = Path.Combine(Path.GetTempPath(), $"LiteMonitor_Task_{Guid.NewGuid()}.xml");
                    
                    // 生成 XML 内容
                    string xmlContent = GetTaskXml(exePath);
                    
                    // 写入临时文件
                    File.WriteAllText(tempXmlPath, xmlContent);

                    // 调用 schtasks 导入 XML
                    // /F: 强制覆盖
                    // /TN: 任务名
                    // /XML: 指定配置文件
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"{TaskName}\" /XML \"{tempXmlPath}\" /F",
                        CreateNoWindow = true,
                        UseShellExecute = false // 必须为 false 才能配合 CreateNoWindow 隐藏窗口
                    };
                    
                    using (var p = Process.Start(startInfo))
                    {
                        p?.WaitForExit();
                        
                        // 可选：检查退出码，如果非0则记录日志，但不建议弹窗打扰用户，除非调试
                        // if (p.ExitCode != 0) { ... }
                    }

                    // 立即清理临时文件
                    if (File.Exists(tempXmlPath)) File.Delete(tempXmlPath);
                }
                catch (Exception ex)
                {
                    // 捕获所有 IO 或 进程异常
                    MessageBox.Show($"设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // 删除任务 (逻辑保持不变)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(startInfo))
                {
                    p?.WaitForExit();
                }
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{TaskName}\"")
                {
                    CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 生成 XML 配置：完美复刻原始逻辑 + 增加高级电池/延迟设置
        /// </summary>
        private static string GetTaskXml(string exePath)
        {
            // 细节保留：获取工作目录，对应你原始代码的 /STRTIN
            string exeDir = Path.GetDirectoryName(exePath)!;
            
            // 获取当前用户，对应原始代码的 /RU
            string userId = WindowsIdentity.GetCurrent().Name;

            // ★★★ 细节处理：XML 特殊字符转义 ★★★
            // 你的原始代码只处理了引号，这里必须处理 XML 敏感字符 (& < > " ')
            // 否则如果路径里包含 '&' (例如 D:\Tom&Jerry\App.exe)，任务会创建失败
            string safeExePath = EscapeXml(exePath);
            string safeExeDir = EscapeXml(exeDir);
            
            // 下面的 XML 结构对应了你原始代码的所有参数：
            // /RL HIGHEST -> <RunLevel>HighestAvailable</RunLevel>
            // /IT -> <LogonType>InteractiveToken</LogonType>
            // /SC ONLOGON -> <LogonTrigger>
            
            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>LiteMonitor Auto Start</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{userId}</UserId>
      <Delay>PT2S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{userId}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{safeExePath}</Command>
      <WorkingDirectory>{safeExeDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        }

        private static string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("&", "&amp;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;");
        }
    }
}