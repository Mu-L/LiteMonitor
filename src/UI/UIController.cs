using LiteMonitor.src.Core;
using LiteMonitor.src.System;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiteMonitor
{
    public class UIController : IDisposable
    {
        private readonly Settings _cfg;
        private readonly Form _form;
        private readonly HardwareMonitor _mon;
        private readonly System.Windows.Forms.Timer _timer;
        private bool _dragging = false;
        private UILayout? _layout;
        private bool _layoutDirty = true;

        private List<GroupLayoutInfo> _groups = new();

        public UIController(Settings cfg, Form form)
        {
            _cfg = cfg;
            _form = form;
            _mon = new HardwareMonitor(cfg);
            _mon.OnValuesUpdated += () => _form.Invalidate();

            _timer = new System.Windows.Forms.Timer { Interval = Math.Max(100, _cfg.RefreshMs) };
            _timer.Tick += (_, __) => Tick();
            _timer.Start();

            // 初始化主题与语言的唯一入口
            ApplyTheme(cfg.Skin);
        }

        // ========== 主题切换 ==========
        public void ApplyTheme(string name)
        {
            // 语言 + 主题的唯一入口
            LanguageManager.Load(_cfg.Language);
            ThemeManager.Load(name);

            // 换主题需清理绘制缓存（第③步会新增该方法）
            UIRenderer.ClearCache();

            var t = ThemeManager.Current;

            // 🟡 新增：DPI 缩放

            float scale = _form.DeviceDpi / 96f;

            var l = t.Layout;

            l.Width = (int)(l.Width * scale);
            l.RowHeight = (int)(l.RowHeight * scale);
            l.Padding = (int)(l.Padding * scale);
            l.GroupPadding = (int)(l.GroupPadding * scale);
            l.GroupSpacing = (int)(l.GroupSpacing * scale);
            l.GroupBottom = (int)(l.GroupBottom * scale);
            l.GroupTitleOffset = (int)(l.GroupTitleOffset * scale);
            l.ItemGap = (int)(l.ItemGap * scale);
            l.CornerRadius = (int)(l.CornerRadius * scale);
            l.GroupRadius = (int)(l.GroupRadius * scale);

            // panel width 也要放大
            // ✅ 修复点：同步主题宽度或设置里的面板宽度
            if (_cfg.PanelWidth > 100)
            {
                t.Layout.Width = (int)(_cfg.PanelWidth * scale);
            }
            else
            {
                t.Layout.Width = (int)(t.Layout.Width * scale);
            }
            _form.Width = t.Layout.Width;

            // ✅ 修复点：切主题时同步窗体背景色，避免边缘露底色
            _form.BackColor = ThemeManager.ParseColor(t.Color.Background);

            // ✅ 重新创建布局对象
            _layout = new UILayout(t);
            _layoutDirty = true;

            // ✅ 重建硬件项列表
            BuildMetrics();

            // ❌ 原逻辑：仅逐组刷新，无法覆盖边缘
            // foreach (var g in _groups)
            //     _form.Invalidate(g.Bounds, false);

            // ✅ 修复点：改为整窗重绘，避免上/左边缘出现白线
            _form.Invalidate();     // 全部客户区
            _form.Update();         // 立即刷新（确保即时重绘）

            // ✅ 可选触发圆角刷新（防止主题宽度变化时圆角不同步）
            // _form.ApplyRoundedCorners();  // 如你的 MainForm 暴露了此方法可打开此行
        }



        public void SetDragging(bool dragging) => _dragging = dragging;

        private bool _busy = false;

        private async void Tick()
        {
            if (_dragging || _busy) return;
            _busy = true;

            try
            {
                await System.Threading.Tasks.Task.Run(() => _mon.UpdateAll());

                foreach (var g in _groups)
                    foreach (var it in g.Items)
                    {
                        it.Value = _mon.Get(it.Key);

                        it.TickSmooth(_cfg.AnimationSpeed);
                    }

                _form.Invalidate();
            }
            finally
            {
                _busy = false;
            }
        }


        // ========== 动态构建分组与项目 ==========
        private void BuildMetrics()
        {
            var t = ThemeManager.Current;
            _groups = new List<GroupLayoutInfo>();

            // === CPU ===
            var cpuItems = new List<MetricItem>();
            if (_cfg.Enabled.CpuLoad)
                cpuItems.Add(new MetricItem { Key = "CPU.Load", Label = LanguageManager.T("Items.CPU.Load") });
            if (_cfg.Enabled.CpuTemp)
                cpuItems.Add(new MetricItem { Key = "CPU.Temp", Label = LanguageManager.T("Items.CPU.Temp") });
            if (cpuItems.Count > 0)
                _groups.Add(new GroupLayoutInfo("CPU", cpuItems));

            // === GPU ===
            var gpuItems = new List<MetricItem>();
            if (_cfg.Enabled.GpuLoad)
                gpuItems.Add(new MetricItem { Key = "GPU.Load", Label = LanguageManager.T("Items.GPU.Load") });
            if (_cfg.Enabled.GpuTemp)
                gpuItems.Add(new MetricItem { Key = "GPU.Temp", Label = LanguageManager.T("Items.GPU.Temp") });
            if (_cfg.Enabled.GpuVram)
                gpuItems.Add(new MetricItem { Key = "GPU.VRAM", Label = LanguageManager.T("Items.GPU.VRAM") });
            if (gpuItems.Count > 0)
                _groups.Add(new GroupLayoutInfo("GPU", gpuItems));

            // === 内存 ===
            var memItems = new List<MetricItem>();
            if (_cfg.Enabled.MemLoad)
                memItems.Add(new MetricItem { Key = "MEM.Load", Label = LanguageManager.T("Items.MEM.Load") });
            if (memItems.Count > 0)
                _groups.Add(new GroupLayoutInfo("MEM", memItems));

            // === 磁盘 ===
            var diskItems = new List<MetricItem>();
            if (_cfg.Enabled.DiskRead)
                diskItems.Add(new MetricItem { Key = "DISK.Read", Label = LanguageManager.T("Items.DISK.Read") });
            if (_cfg.Enabled.DiskWrite)
                diskItems.Add(new MetricItem { Key = "DISK.Write", Label = LanguageManager.T("Items.DISK.Write") });
            if (diskItems.Count > 0)
                _groups.Add(new GroupLayoutInfo("DISK", diskItems));

            // === 网络 ===
            var netItems = new List<MetricItem>();
            if (_cfg.Enabled.NetUp)
                netItems.Add(new MetricItem { Key = "NET.Up", Label = LanguageManager.T("Items.NET.Up") });
            if (_cfg.Enabled.NetDown)
                netItems.Add(new MetricItem { Key = "NET.Down", Label = LanguageManager.T("Items.NET.Down") });
            if (netItems.Count > 0)
                _groups.Add(new GroupLayoutInfo("NET", netItems));

        }

        // ========== 绘制接口 ==========
        public void Render(Graphics g)
        {
            var t = ThemeManager.Current;
            _layout ??= new UILayout(t);

            if (_layoutDirty)
            {
                int contentH = _layout.Build(_groups);   // ← Build 返回内容高度
                _layoutDirty = false;
                _form.Height = contentH + t.Layout.Padding;
            }

            UIRenderer.Render(g, _groups, t);

        }

        // ========== 清理 ==========
        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            _mon.Dispose();
        }
    }
}
