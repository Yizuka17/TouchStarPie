<div align="center">

<img src="./assets/logo.png" width="110" height="110" alt="StarPie Logo" />

# StarPie

### Lightweight, Fast & Configurable Radial Pie Menu for Windows 10 / 11

[![Release Version](https://img.shields.io/badge/Preview-v2.0.0--preview.2-2563EB.svg?style=flat-square&logo=github)](https://github.com/SoftBlack42/StarPie/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64%20%7C%20ARM64)-0078D4.svg?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET%208-WinUI%203-512BD4.svg?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![Tests](https://img.shields.io/badge/Touch%20Core-5%2F5%20Passed-success.svg?style=flat-square&logo=dotnet)](tests/StarPie.WinUI.InputTests/)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20zh--TW%20%7C%20en%20%7C%20ja-8B5CF6.svg?style=flat-square)](#i18n)
[![Co-Authored](https://img.shields.io/badge/Co--Authored%20with-AI%20Agent-6366F1.svg?style=flat-square&logo=openai)](#acknowledgements)

<br/>

**[简体中文](README.md)** • **[English](README_EN.md)**

<br/>

[🚀 Quick Start](#download) • [✨ Key Features](#features) • [🎨 Visual Customization](#visuals) • [🌐 i18n](#i18n) • [🛠️ Build & Dev](#build) • [💡 Story & Maintenance](#acknowledgements) • [📋 Changelog](CHANGELOG.md)

</div>

---

## <a id="intro"></a>📖 Introduction

**StarPie** is a native WinUI 3 radial mouse and multi-touch gesture tool (Pie Menu) for Windows 10 and 11.

Hold and drag the right mouse button in any application to summon a fast radial menu right under your cursor. It supports 4 / 8 / 12 sector layouts, per-application profiles, hotkey recordings, quick app launching, and comprehensive visual styling.

> 💡 **Design Highlights**:
> - **Native Desktop UI**: .NET 8 + Windows App SDK / WinUI 3, with no embedded browser engine;
> - **Low Latency Response**: Built on Win32 `WH_MOUSE_LL` hooks, ensuring fast response without affecting native right-click actions;
> - **Multi-touch**: One-, two-, or three-finger long press followed by a 4-way or 8-way directional swipe;
> - **System Styling**: Automatically follows Windows light/dark mode and accent color;
> - **Portable & Standalone**: Self-contained single executable available (no external runtime installation required);
> - **Single-Instance Protection**: Global mutex prevents duplicate running instances.

> ⚠️ **v2 preview:** the main executable is now true WinUI 3. The touch core, radial control, theme integration, mouse trigger, and tray runtime are available; some advanced v1.6.8 settings panels are still being migrated. Desktop-wide touch capture requires an Authenticode-signed `uiAccess=true` release installed in a trusted location. See [WinUI 3 and touch architecture](docs/WINUI3_TOUCH.md).

---

## <a id="features"></a>✨ Key Features

### 1. ⚡ Quick Mouse Gesture Summon & Execution
- Hold and drag the right mouse button to summon the radial menu. Release over any sector to trigger its configured action (hotkey, application launch, folder opening, or system action);
- Normal single right-clicks pass through to display default Windows context menus cleanly.

<div align="center">
  <img src="./attachments/01_radial_summon.gif" width="680" alt="Quick Radial Gesture Summon Demo" />
  <br/><br/>
  <img src="./attachments/01_1.gif" width="680" alt="Real-world Radial Gesture Interaction Demo" />
</div>

---

### 2. 🚀 Outer Escape Cancel
- To abort an action, you don't need to drag back to the center core;
- Simply flick or overshoot outwards past the wheel boundary. The menu enters a translucent cancel state and releasing the button triggers nothing;
- Configurable toggle switch and adjustable distance threshold slider (140px ~ 320px) provided in settings.

<div align="center">
  <img src="./attachments/02_outer_escape_cancel.gif" width="680" alt="Outer Escape Cancel Demo" />
</div>

---

### <a id="visuals"></a>3. 🎨 Multiple Shapes & Theme Presets
- **4 Geometric Shapes**: Original Compact, Floating Circle, Rounded Capsule, and Hexagon Hive;
- **Built-in Presets**: System Auto, Light, Dark, Glassmorphism, Matcha Forest, Glacial Ice, and Morandi Muted;
- Includes an interactive **Live Preview Canvas** on the right side of the preferences window.

<div align="center">
  <img src="./attachments/03_themes_and_shapes.gif" width="680" alt="Themes and Shapes Customization Demo" />
  <br/><br/>
  <img src="./attachments/03_1.gif" width="680" alt="Multiple Shapes and Layout Showcase" />
  <br/><br/>
  <img src="./attachments/03_2.gif" width="680" alt="Theme Presets and Live Canvas Preview Demo" />
</div>

---

### 4. 🎨 Advanced Color Tuning & Preset Management
- Collapsible fine-tuning panel for sector background, glow, borders, and text colors;
- Supports hex input, color dialog, and screen eyedropper;
- Save, rename, or delete custom color presets anytime.

<div align="center">
  <img src="./attachments/04_custom_colors.gif" width="680" alt="Custom Color Tuning Demo" />
</div>

---

### 5. 🖼️ Custom Vector & Image Icon Imports
- Import custom **SVG vector files** or **PNG / ICO / JPG** images into your icon library;
- Custom icons are stored locally and can be renamed or deleted easily.

<div align="center">
  <img src="./attachments/05_custom_icons.gif" width="680" alt="Custom Icon Import Demo" />
</div>

---

### 6. 🎯 Dynamic 4 / 8 / 12 Sector Layouts
- **4-Sector Layout**: Large cardinal angles, ideal for high-speed blind navigation;
- **8-Sector Layout**: Balanced 8-directional layout (default);
- **12-Sector Layout**: High-density action mapping for complex workflows.

<div align="center">
  <img src="./attachments/06_sector_counts.gif" width="680" alt="4/8/12 Sector Adaptation Demo" />
</div>

---

### 7. 💼 Per-App Profiles & Program Picker
- Create dedicated gesture profiles for apps like Chrome, VS Code, Photoshop, or CAD;
- Integrated application scanner scans installed software and filters stale shortcuts;
- Supports creating, copying, deleting, and renaming profiles.

<div align="center">
  <img src="./attachments/07_per_app_profiles.gif" width="680" alt="Per-App Profiles Demo" />
  <br/><br/>
  <img src="./attachments/07_1.gif" width="680" alt="Application Scanner and Profile Switching Demo" />
</div>

---

### 8. 🛡️ Gaming Isolation, Safety & Multilingual Support
- **Fullscreen & Game Detection**: Automatically bypasses gestures during fullscreen games;
- **Modifier Key Passthrough**: Bypass gestures when holding Ctrl, Shift, or Alt;
- **Blacklist**: Add specific processes to bypass list;
- **Multilingual Support**: Supports Simplified Chinese, Traditional Chinese, English, and Japanese with instant hot switching.

<div align="center">
  <img src="./attachments/08_settings_and_i18n.gif" width="680" alt="Safety and Multilingual Demo" />
</div>

---

## <a id="download"></a>🚀 Download & Quick Start

### Latest stable: `v1.6.8` · WinUI 3 preview: `v2.0.0-preview.2`

| Package | Recommended For | Description | Download |
| :--- | :--- | :--- | :--- |
| **Standalone Single File (Recommended)** | All Users | Built-in .NET runtime, zero external dependencies | [⬇️ Download StarPie.exe](https://github.com/SoftBlack42/StarPie/releases) |
| **Lightweight Portable Package** | Users with .NET 8 installed | Small archive size, portable | [⬇️ Download Lightweight Zip](https://github.com/SoftBlack42/StarPie/releases) |
| **Historical Releases** | Archival & comparison | Previous release builds and notes | [📂 Releases Archive](https://github.com/SoftBlack42/StarPie/releases) |

### Basic Workflow:
1. Run `StarPie.exe` (it stays active in the system tray);
2. Hold and drag the **right mouse button** anywhere to summon the wheel;
3. Release over a sector to trigger the mapped action;
4. Double-click the tray icon to open the configuration window.

---

## <a id="i18n"></a>🌐 Internationalization (i18n)

Switch interface languages anytime in `Advanced & System`:

| Code | Language | Status |
| :--- | :--- | :---: |
| `zh-CN` | 🇨🇳 Simplified Chinese | 🟢 Supported |
| `zh-TW` | 🇭🇰/🇹🇼 Traditional Chinese | 🟢 Supported |
| `en` | 🇺🇸 English | 🟢 Supported |
| `ja` | 🇯🇵 Japanese | 🟢 Supported |
| `Auto` | 🖥️ System Default | 🟢 Supported |

---

## <a id="build"></a>🛠️ Development & Build

### Requirements
- Windows 10 / 11 (x64 / ARM64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run
```bash
git clone https://github.com/SoftBlack42/StarPie.git
cd StarPie
dotnet build WinPieGestures/WinPieGestures.csproj -c Release -p:Platform=x64
dotnet run --project WinPieGestures/WinPieGestures.csproj -p:Platform=x64

# Optional: build the v1.6.8 WPF compatibility baseline
dotnet build WinPieGestures/WinPieGestures.LegacyWpf.csproj -c Release
```

### Touch Core Tests (5/5 Passed 🟢)
```bash
dotnet run --project tests/StarPie.WinUI.InputTests/StarPie.WinUI.InputTests.csproj -c Release
```

---

## <a id="acknowledgements"></a>💡 Story & Maintenance

### 🌟 Inspiration
As a Mechanical Engineering student frequently using CAD tools like SolidWorks, I appreciated the convenience of mouse gesture radial menus. With the help of AI Agent tools, I wanted to bring this interaction model to the wider Windows desktop environment.

Feedback, bug reports, and pull requests are always welcome!

### 🤖 AI Collaboration
Designed and architected by the developer, with implementation and test automation co-authored with **AI Agent - Antigravity**.

### 📌 Maintenance Note
v1.6.8 remains the WPF stable baseline. v2.0.0-preview.2 is the WinUI 3 and multi-touch migration preview.

---

## <a id="license"></a>📄 License

Licensed under the [MIT License](LICENSE).
