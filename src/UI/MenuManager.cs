using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using LiteMonitor.src.SystemServices;
using LiteMonitor.src.Core;
using System.Collections.Generic;

namespace LiteMonitor
{
    public static class MenuManager
    {
        // [已删除] EnsureAtLeastOneVisible 方法已移入 src/Core/AppActions.cs 的 ApplyVisibility 中

        /// <summary>
        /// 构建 LiteMonitor 主菜单（右键菜单 + 托盘菜单）
        /// </summary>
        public static ContextMenuStrip Build(MainForm form, Settings cfg, UIController? ui)
        {
            var menu = new ContextMenuStrip();

            // ==================================================================================
            // 1. 基础功能区 (置顶、显示模式、任务栏开关、隐藏主界面/托盘)
            // ==================================================================================

            // === 置顶 ===
            var topMost = new ToolStripMenuItem(LanguageManager.T("Menu.TopMost"))
            {
                Checked = cfg.TopMost,
                CheckOnClick = true
            };
            topMost.CheckedChanged += (_, __) =>
            {
                cfg.TopMost = topMost.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyWindowAttributes(cfg, form);
            };
            menu.Items.Add(topMost);
            menu.Items.Add(new ToolStripSeparator());

            // === 显示模式 ===
            var modeRoot = new ToolStripMenuItem(LanguageManager.T("Menu.DisplayMode"));

            var vertical = new ToolStripMenuItem(LanguageManager.T("Menu.Vertical"))
            {
                Checked = !cfg.HorizontalMode
            };
            var horizontal = new ToolStripMenuItem(LanguageManager.T("Menu.Horizontal"))
            {
                Checked = cfg.HorizontalMode
            };

            // 辅助点击事件
            void SetMode(bool isHorizontal)
            {
                cfg.HorizontalMode = isHorizontal;
                cfg.Save();
                // ★ 统一调用 (含主题、布局刷新)
                AppActions.ApplyThemeAndLayout(cfg, ui, form);
            }

            vertical.Click += (_, __) => SetMode(false);
            horizontal.Click += (_, __) => SetMode(true);

            modeRoot.DropDownItems.Add(vertical);
            modeRoot.DropDownItems.Add(horizontal);
            modeRoot.DropDownItems.Add(new ToolStripSeparator());

            // === 任务栏显示 ===
            var taskbarMode = new ToolStripMenuItem(LanguageManager.T("Menu.TaskbarShow"))
            {
                Checked = cfg.ShowTaskbar
            };

            taskbarMode.Click += (_, __) =>
            {
                cfg.ShowTaskbar = !cfg.ShowTaskbar;
                // 保存
                cfg.Save(); 
                // ★ 统一调用 (含防呆检查、显隐逻辑、菜单刷新)
                AppActions.ApplyVisibility(cfg, form);
            };

            modeRoot.DropDownItems.Add(taskbarMode);
            modeRoot.DropDownItems.Add(new ToolStripSeparator());



            // === 自动隐藏 ===
            var autoHide = new ToolStripMenuItem(LanguageManager.T("Menu.AutoHide"))
            {
                Checked = cfg.AutoHide,
                CheckOnClick = true
            };
            autoHide.CheckedChanged += (_, __) =>
            {
                cfg.AutoHide = autoHide.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyWindowAttributes(cfg, form);
            };
            modeRoot.DropDownItems.Add(autoHide);

            // === 限制窗口拖出屏幕 (纯数据开关) ===
            var clampItem = new ToolStripMenuItem(LanguageManager.T("Menu.ClampToScreen"))
            {
                Checked = cfg.ClampToScreen,
                CheckOnClick = true
            };
            clampItem.CheckedChanged += (_, __) =>
            {
                cfg.ClampToScreen = clampItem.Checked;
                cfg.Save();
            };
            modeRoot.DropDownItems.Add(clampItem);

            // === 鼠标穿透 ===
            var clickThrough = new ToolStripMenuItem(LanguageManager.T("Menu.ClickThrough"))
            {
                Checked = cfg.ClickThrough,
                CheckOnClick = true
            };
            clickThrough.CheckedChanged += (_, __) =>
            {
                cfg.ClickThrough = clickThrough.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyWindowAttributes(cfg, form);
            };
            modeRoot.DropDownItems.Add(clickThrough);

            modeRoot.DropDownItems.Add(new ToolStripSeparator());

            
           

            // === 透明度 ===
            var opacityRoot = new ToolStripMenuItem(LanguageManager.T("Menu.Opacity"));
            double[] presetOps = { 1.0, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.6, 0.5, 0.4, 0.3 };
            foreach (var val in presetOps)
            {
                var item = new ToolStripMenuItem($"{val * 100:0}%")
                {
                    Checked = Math.Abs(cfg.Opacity - val) < 0.01
                };

                item.Click += (_, __) =>
                {
                    cfg.Opacity = val;
                    cfg.Save();
                    // ★ 统一调用
                    AppActions.ApplyWindowAttributes(cfg, form);
                };
                opacityRoot.DropDownItems.Add(item);
            }
            modeRoot.DropDownItems.Add(opacityRoot);

            // === 界面宽度 ===
            var widthRoot = new ToolStripMenuItem(LanguageManager.T("Menu.Width"));
            int[] presetWidths = { 180, 200, 220, 240, 260, 280, 300, 360, 420, 480, 540, 600, 660, 720, 780, 840, 900, 960, 1020, 1080, 1140, 1200 };
            int currentW = cfg.PanelWidth;

            foreach (var w in presetWidths)
            {
                var item = new ToolStripMenuItem($"{w}px")
                {
                    Checked = Math.Abs(currentW - w) < 1
                };
                item.Click += (_, __) =>
                {
                    cfg.PanelWidth = w;
                    cfg.Save();
                    // ★ 统一调用
                    AppActions.ApplyThemeAndLayout(cfg, ui, form);
                };
                widthRoot.DropDownItems.Add(item);
            }
            modeRoot.DropDownItems.Add(widthRoot);

            // === 界面缩放 ===
            var scaleRoot = new ToolStripMenuItem(LanguageManager.T("Menu.Scale"));
            (double val, string key)[] presetScales =
            {
                (2.00, "200%"), (1.75, "175%"), (1.50, "150%"), (1.25, "125%"),
                (1.00, "100%"), (0.90, "90%"),  (0.85, "85%"),  (0.80, "80%"),
                (0.75, "75%"),  (0.70, "70%"),  (0.60, "60%"),  (0.50, "50%")
            };

            double currentScale = cfg.UIScale;
            foreach (var (scale, label) in presetScales)
            {
                var item = new ToolStripMenuItem(label)
                {
                    Checked = Math.Abs(currentScale - scale) < 0.01
                };

                item.Click += (_, __) =>
                {
                    cfg.UIScale = scale;
                    cfg.Save();
                    // ★ 统一调用
                    AppActions.ApplyThemeAndLayout(cfg, ui, form);
                };
                scaleRoot.DropDownItems.Add(item);
            }

            modeRoot.DropDownItems.Add(scaleRoot);
            modeRoot.DropDownItems.Add(new ToolStripSeparator());


            
             // === 隐藏主窗口 ===
            var hideMainForm = new ToolStripMenuItem(LanguageManager.T("Menu.HideMainForm"))
            {
                Checked = cfg.HideMainForm,
                CheckOnClick = true
            };

            hideMainForm.CheckedChanged += (_, __) =>
            {
                cfg.HideMainForm = hideMainForm.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyVisibility(cfg, form);
            };
            modeRoot.DropDownItems.Add(hideMainForm);


             // === 隐藏托盘图标 ===
            var hideTrayIcon = new ToolStripMenuItem(LanguageManager.T("Menu.HideTrayIcon"))
            {
                Checked = cfg.HideTrayIcon,
                CheckOnClick = true
            };

            hideTrayIcon.CheckedChanged += (_, __) =>
            {
                // 注意：旧的 CheckIfAllowHide 逻辑已整合进 AppActions.ApplyVisibility 的防呆检查中
                // 这里只需修改配置并调用 Action 即可
                
                cfg.HideTrayIcon = hideTrayIcon.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyVisibility(cfg, form);
            }; 
            modeRoot.DropDownItems.Add(hideTrayIcon);
            menu.Items.Add(modeRoot);



            // ==================================================================================
            // 2. 显示监控项 (动态生成)
            // ==================================================================================

            var grpShow = new ToolStripMenuItem(LanguageManager.T("Menu.ShowItems"));
            menu.Items.Add(grpShow);

            // --- 内部辅助函数：最大值引导提示 (保留 UI 交互逻辑) ---
            void CheckAndRemind(string name)
            {
                if (cfg.MaxLimitTipShown) return;

                string msg = cfg.Language == "zh"
                    ? $"您是首次开启 {name}。\n\n建议设置一下电脑实际“最大{name}”，让进度条显示更准确。\n\n是否现在去设置？\n\n点“否”将不再提示，程序将在高负载时（如大型游戏时）进行动态学习最大值"
                    : $"You are enabling {name} for the first time.\n\nIt is recommended to set the actual 'Max {name}' for accurate progress bars.\n\nGo to settings now?\n\n(Select 'No' to suppress this prompt. The app will auto-learn the max value under high load.)";

                cfg.MaxLimitTipShown = true;
                cfg.Save();

                if (MessageBox.Show(msg, "LiteMonitor Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        // 打开设置窗口
                        using (var f = new LiteMonitor.src.UI.SettingsForm(cfg, ui, form))
                        {
                            f.SwitchPage("System"); 

                            f.ShowDialog(form);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("设置面板启动失败: " + ex.Message);
                    }
                }
            }

            // --- 动态遍历 MonitorItems 列表 ---
            var sortedItems = cfg.MonitorItems.OrderBy(x => x.SortIndex).ToList();
            
            // 定义需要“整组合并”的特殊组
            var unifiedGroups = new HashSet<string> { "DISK", "NET", "DATA" };
            var processedGroups = new HashSet<string>();

            // ★ 新增：用于记录上一个组名，判断是否需要加下划线
            string lastGroup = null;

            foreach (var itemConfig in sortedItems)
            {
                // 获取组前缀 (CPU, GPU, DISK, NET, DATA...)
                string groupKey = itemConfig.Key.Split('.')[0];

                // 如果是已处理的特殊组，直接跳过（避免重复显示）
                if (unifiedGroups.Contains(groupKey) && processedGroups.Contains(groupKey)) continue;

                // ★★★ 核心修改：如果组名变了（且不是第一项），就加一条下划线 ★★★
                if (lastGroup != null && groupKey != lastGroup)
                {
                    grpShow.DropDownItems.Add(new ToolStripSeparator());
                }
                lastGroup = groupKey; // 更新记录

                // =========================================================
                // 情况 A：特殊组 (Disk/Net/Traffic) -> 合并显示为一个开关
                // =========================================================
                if (unifiedGroups.Contains(groupKey))
                {
                    processedGroups.Add(groupKey); // 标记该组已处理

                    // 获取该组下的所有监控项
                    var groupItems = sortedItems.Where(x => x.Key.StartsWith(groupKey + ".")).ToList();
                    
                    // 获取组名称
                    string groupLabel = LanguageManager.T("Groups." + groupKey);
                    if (string.IsNullOrEmpty(groupLabel)) groupLabel = groupKey;

                    // 状态判断
                    bool isAnyVisible = groupItems.Any(x => x.VisibleInPanel);

                    var groupMenu = new ToolStripMenuItem(groupLabel)
                    {
                        Checked = isAnyVisible,
                        CheckOnClick = true
                    };

                    groupMenu.CheckedChanged += (_, __) =>
                    {
                        bool newState = groupMenu.Checked;
                        foreach (var gItem in groupItems)
                        {
                            gItem.VisibleInPanel = newState;
                        }
                        cfg.Save();
                        AppActions.ApplyMonitorLayout(ui, form);
                    };
                    grpShow.DropDownItems.Add(groupMenu);
                }
                // =========================================================
                // 情况 B：普通组 (CPU/GPU/MEM) -> 直接扁平化显示单项
                // =========================================================
                else
                {
                    string label = !string.IsNullOrEmpty(itemConfig.UserLabel)
                        ? itemConfig.UserLabel
                        : LanguageManager.T("Items." + itemConfig.Key);
                    if (string.IsNullOrEmpty(label)) label = itemConfig.Key;

                    var menuItem = new ToolStripMenuItem(label)
                    {
                        Checked = itemConfig.VisibleInPanel,
                        CheckOnClick = true
                    };

                    menuItem.CheckedChanged += (_, __) =>
                    {
                        itemConfig.VisibleInPanel = menuItem.Checked;
                        cfg.Save();
                        AppActions.ApplyMonitorLayout(ui, form);

                        if (menuItem.Checked)
                        {
                            if (itemConfig.Key.Contains("Clock") || itemConfig.Key.Contains("Power"))
                            {
                                CheckAndRemind(label);
                            }
                        }
                    };
                    grpShow.DropDownItems.Add(menuItem);
                }
            }

            menu.Items.Add(new ToolStripSeparator());

            // ==================================================================================
            // 3. 主题、工具与更多功能
            // ==================================================================================

            // === 主题 ===
            var themeRoot = new ToolStripMenuItem(LanguageManager.T("Menu.Theme"));
            // 主题编辑器 (独立窗口，保持原样)
            var themeEditor = new ToolStripMenuItem(LanguageManager.T("Menu.ThemeEditor"));
            themeEditor.Image = Properties.Resources.ThemeIcon;
            themeEditor.Click += (_, __) => new ThemeEditor.ThemeEditorForm().Show();
            themeRoot.DropDownItems.Add(themeEditor);
            themeRoot.DropDownItems.Add(new ToolStripSeparator());

            foreach (var name in ThemeManager.GetAvailableThemes())
            {
                var item = new ToolStripMenuItem(name)
                {
                    Checked = name.Equals(cfg.Skin, StringComparison.OrdinalIgnoreCase)
                };

                item.Click += (_, __) =>
                {
                    cfg.Skin = name;
                    cfg.Save();
                    // ★ 统一调用
                    AppActions.ApplyThemeAndLayout(cfg, ui, form);
                };
                themeRoot.DropDownItems.Add(item);
            }
            menu.Items.Add(themeRoot);
            menu.Items.Add(new ToolStripSeparator());

            // 网络测速 (独立窗口，保持原样)
            var speedWindow = new ToolStripMenuItem(LanguageManager.T("Menu.Speedtest"));
            speedWindow.Image = Properties.Resources.NetworkIcon;
            speedWindow.Click += (_, __) =>
            {
                var f = new SpeedTestForm();
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(form.Left + 20, form.Top + 20);
                f.Show();
            };
            menu.Items.Add(speedWindow);


            // 历史流量统计 (独立窗口，保持原样)
            var trafficItem = new ToolStripMenuItem(LanguageManager.T("Menu.Traffic"));
            trafficItem.Image = Properties.Resources.TrafficIcon;
            trafficItem.Click += (_, __) =>
            {
                var formHistory = new TrafficHistoryForm(cfg);
                formHistory.Show();
            };
            menu.Items.Add(trafficItem);

             // =================================================================
            // [新增] 设置中心入口
            // =================================================================
            var itemSettings = new ToolStripMenuItem(LanguageManager.T("Menu.SettingsPanel")); 
            itemSettings.Image = Properties.Resources.Settings;
            
            // 临时写死中文，等面板做完善了再换成 LanguageManager.T("Menu.Settings")
            
            itemSettings.Font = new Font(itemSettings.Font, FontStyle.Bold); 

            itemSettings.Click += (_, __) =>
            {
                try
                {
                    // 打开设置窗口
                    using (var f = new LiteMonitor.src.UI.SettingsForm(cfg, ui, form))
                    {
                        f.ShowDialog(form);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("设置面板启动失败: " + ex.Message);
                }
            };
            menu.Items.Add(itemSettings);
            
            menu.Items.Add(new ToolStripSeparator());

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // === 语言切换 ===
            var langRoot = new ToolStripMenuItem(LanguageManager.T("Menu.Language"));
            string langDir = Path.Combine(AppContext.BaseDirectory, "resources/lang");

            if (Directory.Exists(langDir))
            {
                foreach (var file in Directory.EnumerateFiles(langDir, "*.json"))
                {
                    string code = Path.GetFileNameWithoutExtension(file);

                    var item = new ToolStripMenuItem(code.ToUpper())
                    {
                        Checked = cfg.Language.Equals(code, StringComparison.OrdinalIgnoreCase)
                    };

                    item.Click += (_, __) =>
                    {
                        cfg.Language = code;
                        cfg.Save();
                        // ★ 统一调用
                        AppActions.ApplyLanguage(cfg, ui, form);
                    };

                    langRoot.DropDownItems.Add(item);
                }
            }

            menu.Items.Add(langRoot);
            menu.Items.Add(new ToolStripSeparator());

            // === 开机启动 ===
            var autoStart = new ToolStripMenuItem(LanguageManager.T("Menu.AutoStart"))
            {
                Checked = cfg.AutoStart,
                CheckOnClick = true
            };
            autoStart.CheckedChanged += (_, __) =>
            {
                cfg.AutoStart = autoStart.Checked;
                cfg.Save();
                // ★ 统一调用
                AppActions.ApplyAutoStart(cfg);
            };
            menu.Items.Add(autoStart);

            // === 关于 ===
            var about = new ToolStripMenuItem(LanguageManager.T("Menu.About"));
            about.Click += (_, __) => new AboutForm().ShowDialog(form);
            menu.Items.Add(about);

            menu.Items.Add(new ToolStripSeparator());

            // === 退出 ===
            var exit = new ToolStripMenuItem(LanguageManager.T("Menu.Exit"));
            exit.Click += (_, __) => form.Close();
            menu.Items.Add(exit);

            return menu;
        }
    }
}