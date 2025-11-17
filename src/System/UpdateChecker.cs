using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiteMonitor.src.System
{
    public static class UpdateChecker
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/Diorser/LiteMonitor/master/resources/version.json";

        public static async Task CheckAsync(bool showMessage = false)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var json = await http.GetStringAsync(VersionUrl);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latest = Normalize(root.GetProperty("version").GetString());
                string changelog = root.TryGetProperty("changelog", out var c) ? c.GetString() ?? "" : "";
                string releaseDate = root.TryGetProperty("releaseDate", out var r) ? r.GetString() ?? "" : "";
                string downloadUrl = root.TryGetProperty("downloadUrl", out var d) ? d.GetString() ?? "" : "";

                // 当前版本 - 使用 <Version>（InformationalVersion）
                string current = typeof(Program).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion
                    ?? Application.ProductVersion
                    ?? "0.0.0";

                current = Normalize(current);

                // 是否发现新版本
                if (IsNewer(latest, current))
                {
                    string msg =$"发现新版本：{latest}\n发布日期：{releaseDate}\n更新内容：{changelog}\n是否前往下载？\n\n当前版本：{current}\n";

                    if (MessageBox.Show(msg, "LiteMonitor 更新",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        if (!string.IsNullOrWhiteSpace(downloadUrl))
                            Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                    }
                }
                else if (showMessage)
                {
                    MessageBox.Show($"当前已是最新版：{current}", "LiteMonitor",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (showMessage)
                    MessageBox.Show("检查更新失败。\n" + ex.Message, "LiteMonitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    Debug.WriteLine("[UpdateChecker] " + ex.Message);
            }
        }

        private static string Normalize(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            int plus = version.IndexOf('+');
            return plus >= 0 ? version.Substring(0, plus) : version;
        }

        private static bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(latest, out var v1) && Version.TryParse(current, out var v2))
                return v1 > v2;
            return false;
        }
    }
}
