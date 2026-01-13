using LiteMonitor.src.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiteMonitor
{
    public enum LayoutMode
    {
        Horizontal,
        Taskbar
    }

    public class HorizontalLayout
    {
        private readonly Theme _t;
        private readonly LayoutMode _mode;
        private readonly Settings _settings;

        private readonly int _padding;
        private int _rowH;

        // DPI
        private readonly float _dpiScale;

        public int PanelWidth { get; private set; }

        // ====== 保留你原始最大宽度模板（横屏模式用） ======
        private const string MAX_VALUE_NORMAL = "100°C";
        private const string MAX_VALUE_IO = "999KB";
        private const string MAX_VALUE_CLOCK = "99GHz"; 
        private const string MAX_VALUE_POWER = "999W";

        public HorizontalLayout(Theme t, int initialWidth, LayoutMode mode, Settings? settings = null)
        {
            _t = t;
            _mode = mode;
            _settings = settings ?? Settings.Load();

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                _dpiScale = g.DpiX / 96f;
            }

            _padding = t.Layout.Padding;

            if (mode == LayoutMode.Horizontal)
                _rowH = Math.Max(t.FontItem.Height, t.FontValue.Height);
            else
                _rowH = 0; // 任务栏模式稍后根据 taskbarHeight 决定

            PanelWidth = initialWidth;
        }

        /// <summary>
        /// Build：横屏/任务栏共用布局
        /// </summary>
        public int Build(List<Column> cols, int taskbarHeight = 32)
        {
            if (cols == null || cols.Count == 0) return 0;
            // ★ 1. 获取统一样式（不管是自定义还是默认，都由它决定）
            var s = _settings.GetStyle();

            int pad = _padding;
            int padV = _padding / 2;

            // ★ 定义单行模式变量
            bool isTaskbarSingle = (_mode == LayoutMode.Taskbar && _settings.TaskbarSingleLine);

            if (_mode == LayoutMode.Taskbar)
            {
                // 任务栏上下没有额外 padding
                padV = 0;

                // ★ 如果是单行模式，行高=全高；否则=半高
                _rowH = isTaskbarSingle ? taskbarHeight : taskbarHeight / 2;
            }

            // ==== 宽度初始值 ====
            int totalWidth = pad * 2;

            float dpi = _dpiScale;

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                foreach (var col in cols)
                {
                    // ===== label（Top/Bottom 按最大宽度） =====
                    // ★★★ 修复：使用MetricItem中已经缓存的ShortLabel，避免重复创建字符串 ★★★
                    string labelTop = col.Top != null ? col.Top.ShortLabel : "";
                    string labelBottom = col.Bottom != null ? col.Bottom.ShortLabel : "";

                    Font labelFont, valueFont;

                    if (_mode == LayoutMode.Taskbar)
                    {
                        var fs = s.Bold ? FontStyle.Bold : FontStyle.Regular;
                        var f = new Font(s.Font, s.Size, fs); // 直接用 s.Font, s.Size
                        labelFont = f; valueFont = f;
                    }
                    else
                    {
                        labelFont = _t.FontItem;
                        valueFont = _t.FontValue;
                    }

                    int wLabelTop = TextRenderer.MeasureText(
                        g, labelTop, labelFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding
                    ).Width;

                    int wLabelBottom = TextRenderer.MeasureText(
                        g, labelBottom, labelFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding
                    ).Width;

                    int wLabel = Math.Max(wLabelTop, wLabelBottom);

                    // ========== value 最大宽度 ==========
                    string sampleTop = GetMaxValueSample(col, true);
                    string sampleBottom = GetMaxValueSample(col, false);

                    int wValueTop = TextRenderer.MeasureText(
                        g, sampleTop, valueFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding
                    ).Width;

                    int wValueBottom = TextRenderer.MeasureText(
                        g, sampleBottom, valueFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding
                    ).Width;

                    int wValue = Math.Max(wValueTop, wValueBottom);
                    // ★ 3. 替换内间距逻辑
                    int paddingX = _rowH;
                    if (_mode == LayoutMode.Taskbar) paddingX = (int)Math.Round(s.Inner * dpi); // 直接用 s.Inner

                    // ====== 列宽（不再限制最大/最小宽度）======
                    col.ColumnWidth = wLabel + wValue + paddingX;
                    totalWidth += col.ColumnWidth;

                    if (_mode == LayoutMode.Taskbar)
                    {
                        labelFont.Dispose();
                        valueFont.Dispose();
                    }
                }
            }

           
           // ★ 4. 替换组间距逻辑
            int gapBase = (_mode == LayoutMode.Taskbar) ? s.Gap : 12; // 直接用 s.Gap
            int gap = (int)Math.Round(gapBase * dpi); // ===== gap 随 DPI =====

            if (cols.Count > 1) totalWidth += (cols.Count - 1) * gap;
            PanelWidth = totalWidth;
            
            // ===== 设置列 Bounds =====
            int x = pad;

            foreach (var col in cols)
            {
                // ★ 整个列的高度
                int colHeight = isTaskbarSingle ? _rowH : _rowH * 2;
                col.Bounds = new Rectangle(x, padV, col.ColumnWidth, colHeight);

                if (_mode == LayoutMode.Taskbar)
                {
                   // ★ 5. 垂直定位逻辑（包含 offset 修正）
                    int fixOffset = 1; // 全局向下微调 1px 防止偏上
                    
                    if (isTaskbarSingle) {
                        col.BoundsTop = new Rectangle(x, col.Bounds.Y + fixOffset, col.ColumnWidth, colHeight);
                        col.BoundsBottom = Rectangle.Empty;
                    } else {
                        // 双行模式：直接用 s.VOff
                        col.BoundsTop = new Rectangle(x, col.Bounds.Y + s.VOff + fixOffset, col.ColumnWidth, _rowH - s.VOff);
                        col.BoundsBottom = new Rectangle(x, col.Bounds.Y + _rowH - s.VOff + fixOffset, col.ColumnWidth, _rowH);
                    }
                }
                else
                {
                    // 横屏模式 (保持不变)
                    col.BoundsTop = new Rectangle(col.Bounds.X, col.Bounds.Y, col.Bounds.Width, _rowH);
                    col.BoundsBottom = new Rectangle(col.Bounds.X, col.Bounds.Y + _rowH, col.Bounds.Width, _rowH);
                }

                x += col.ColumnWidth + gap;
            }

            // ★ 返回总高度
            return padV * 2 + (isTaskbarSingle ? _rowH : _rowH * 2);
        }

        private string GetMaxValueSample(Column col, bool isTop)
        {
            // ★★★ 优化：移除 ToUpperInvariant() 分配，改用忽略大小写的比较 ★★★
            string key = (isTop ? col.Top?.Key : col.Bottom?.Key) ??
                         (isTop ? col.Bottom?.Key : col.Top?.Key) ?? "";

            // ★★★ 简单匹配，使用 IndexOf 替换 Contains ★★★
            if (key.IndexOf("CLOCK", StringComparison.OrdinalIgnoreCase) >= 0) return MAX_VALUE_CLOCK;
            if (key.IndexOf("POWER", StringComparison.OrdinalIgnoreCase) >= 0) return MAX_VALUE_POWER;

            bool isIO =
                key.IndexOf("READ", StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("WRITE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("UP", StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("DOWN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("DAYUP", StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("DAYDOWN", StringComparison.OrdinalIgnoreCase) >= 0;

            return isIO ? MAX_VALUE_IO : MAX_VALUE_NORMAL;
        }
    }

    public class Column
    {
        public MetricItem? Top;
        public MetricItem? Bottom;

        public int ColumnWidth;
        public Rectangle Bounds = Rectangle.Empty;

        // ★★ B 方案新增：上下行布局由 Layout 计算，不再由 Renderer 处理
        public Rectangle BoundsTop = Rectangle.Empty;
        public Rectangle BoundsBottom = Rectangle.Empty;
    }
}
