[中文文档](./README.md)


# ⚡ LiteMonitor
A lightweight and customizable **Windows hardware monitor** — track your CPU, GPU, memory, disk, and network stats in real time.

> 🟢 **Download the latest version:** [📦 GitHub Releases → LiteMonitor Latest](https://github.com/Diorser/LiteMonitor/releases/latest)


![LiteMonitor Overview](./screenshots/overview.png)

LiteMonitor is a modern, minimal **desktop system monitor** built with **.NET 8 (WinForms)**.  
It offers smooth animations, theme customization, transparency control, and multilingual UI — a lightweight alternative to traditional **traffic and system monitor tools**.

---

# 🖥️ Monitoring Features

| Category | Metrics |
|-----------|----------|
| 💻 **CPU (Processor)** | Monitors real-time CPU usage and temperature with multi-core average and peak tracking. |
| 🎮 **GPU (Graphics Card)** | Displays GPU usage, core temperature, and VRAM utilization. Supports NVIDIA, AMD, and Intel GPUs. |
| 💾 **Memory (RAM)** | Shows current RAM usage in percentage and GB units for quick performance insight. |
| 📀 **Disk (Storage)** | Tracks disk read/write speed (KB/s, MB/s) to analyze storage I/O load. |
| 🌐 **Network (Bandwidth)** | Displays real-time upload and download speed — lightweight network traffic monitoring. |


---

# Product Features

| Feature | Description |
|---|---|
| 🎨 Theme Customization | JSON-defined colors, fonts, spacing, and corner radius; themes are extensible and reusable. |
| 🔴🟡🟢 **Three-Level Color Alerts** | Metric bars and values change color dynamically based on thresholds, with fully customizable colors. |
| 🌍 Multilingual UI | Supports 8 languages (Chinese, English, Japanese, Korean, French, German, Spanish, Russian). Language switch takes effect instantly without restart. |
| 📊 Show/Hide Monitoring Items | Selectively display CPU, GPU, Memory, Disk, and Network modules to focus on what matters. |
| 📏 Adjustable Width | Change panel width from the context menu; layout adapts instantly with no restart. |
| 🪟 Window & UI | Rounded corners, adjustable opacity, “Always on top”, drop shadow, and high-quality text rendering. |
| 🧭 Auto Hide at Screen Edge | Auto-collapses when docked to the edge; reappears on hover to save desktop space. |
| 👆 Click-Through Mode | Lets mouse clicks pass through the panel for seamless interaction with underlying apps. |
| 💫 Smooth Animation | Tunable transition speed for stable, jitter-free value changes. |
| 🧩 Real-time Theme/Language Switch | Switching applies immediately without requiring a restart. |
| 🔠 DPI Scaling | Fonts and layout automatically adapt to system scaling; crisp on high-DPI displays. |
| 📂 Auto-Save Settings | Changes (opacity, theme, language, shown items, etc.) are saved instantly to `settings.json`. |
| 🚀 Auto Start | Launches via Windows Task Scheduler; supports elevated privileges. |
| 🔄 Auto Update Check | Fetches the latest version info and prompts to download releases. |
| ⚙️ Configuration Storage | Centralized user preferences in `settings.json` for easy migration and backup. |


---

## 📦 Installation

1. Download the latest version from [GitHub Releases](https://github.com/Diorser/LiteMonitor/releases)
2. Extract and run `LiteMonitor.exe`
3. The app automatically loads the correct language and theme

---

## 🌐 Multilingual Support

Language files are stored in `/lang/`:

| Language | File |
|-----------|------|
| Chinese (Simplified) | `zh.json` |
| English | `en.json` |
| Japanese | `ja.json` |
| Korean | `ko.json` |
| French | `fr.json` |
| German | `de.json` |
| Spanish | `es.json` |
| Russian | `ru.json` |

---

## 🎨 Theme System

Themes are stored under `/themes/` as JSON files.

Example:
```json
{
  "name": "DarkFlat_Classic",
  "layout": { "rowHeight": 40, "cornerRadius": 10 },
  "color": {
    "background": "#202225",
    "textPrimary": "#EAEAEA",
    "barLow": "#00C853"
  }
}
```

---

## ⚙️ Settings (settings.json)

| Field | Description |
|--------|-------------|
| `Skin` | Current theme name |
| `PanelWidth` | Panel width |
| `Opacity` | Window opacity |
| `Language` | Current language |
| `TopMost` | Always on top |
| `AutoStart` | Run at startup |
| `AutoHide` | Auto-hide when near screen edge |
| `ClickThrough` | Enable mouse click-through |
| `AnimationSpeed` | Smooth animation speed |
| `Enabled` | Show/hide monitoring items |

---

## 🧩 Architecture Overview

| File | Responsibility |
|------|----------------|
| `MainForm_Transparent.cs` | Main window logic, right-click menu, and layout control |
| `UIController.cs` | Theme and update control |
| `UIRenderer.cs` | Rendering of bars, texts, and smooth transitions |
| `UILayout.cs` | Dynamic layout calculation |
| `ThemeManager.cs` | Load and parse theme JSON files |
| `LanguageManager.cs` | Manage language localization files |
| `HardwareMonitor.cs` | Collect system data using LibreHardwareMonitorLib |
| `AutoStart.cs` | Manage Windows Task Scheduler for startup |
| `UpdateChecker.cs` | GitHub version checker |
| `AboutForm.cs` | About window dialog |

---

## 🛠️ Build Instructions

### Requirements
- Windows 10 / 11  
- .NET 8 SDK  
- Visual Studio 2022 or JetBrains Rider

### Build Steps
```bash
git clone https://github.com/Diorser/LiteMonitor.git
cd LiteMonitor
dotnet build -c Release
```

Output:
```
/bin/Release/net8.0-windows/LiteMonitor.exe
```

---

## 📄 License
Released under the **MIT License** — free for commercial and personal use.

---

## 💬 Contact
**Author:** Diorser  
**GitHub:** [https://github.com/Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor)

---

<!-- SEO Keywords: Windows hardware monitor, system monitor, desktop performance widget, traffic monitor alternative, CPU GPU temperature monitor, open-source hardware monitor, lightweight system widget, memory and network usage tracker -->
