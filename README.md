<div align="center">

<img src="./assets/logo.png" width="110" height="110" alt="StarPie Logo" />

# StarPie (星盘)

### 轻量、快捷的 Windows 鼠标轮盘手势与效率工具
**Lightweight, Fast & Configurable Radial Pie Menu for Windows 10 / 11**

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

[🚀 快速开始](#download) • [✨ 功能特性](#features) • [🎨 外观定制](#visuals) • [🌐 多语言](#i18n) • [🛠️ 本地构建](#build) • [💡 开发故事与维护说明](#acknowledgements) • [📋 更新日志](CHANGELOG.md)

</div>

---

## <a id="intro"></a>📖 简介

**StarPie (星盘)** 是一款专为 Windows 10 / 11 打造的原生 WinUI 3 鼠标与多指触摸轮盘（Radial / Pie Menu）效率工具。

在日常使用或专业建模软件中，通过**按住鼠标右键滑动**，即可在光标所在位置呼出快捷轮盘。支持 4 / 8 / 12 方位动作映射、专属程序配置（Per-App Profiles）、快捷键录制、程序极速启动与多种视觉形态定制，帮助将高频操作转化为自然的肌肉记忆。

> 💡 **设计重点**：
> - **原生桌面 UI**：基于 .NET 8 + Windows App SDK / WinUI 3，无浏览器内核；
> - **低延迟响应**：基于 Win32 `WH_MOUSE_LL` 底层事件流，响应迅速，不影响鼠标正常右键点击；
> - **多指触摸**：支持单指/双指/三指长按后 4 向或 8 向划动命中；
> - **系统风格**：轮盘与设置界面自动跟随深色/浅色模式和 Windows 强调色；
> - **绿色便携**：提供独立单文件版（内置 .NET 运行时，解压即用），配置保存于本地 `config.json`；

> ⚠️ **v2 预览说明**：主程序已切换为真 WinUI 3，触摸核心、轮盘、主题、鼠标触发与托盘已可用；v1.6.8 的部分高级配置面板仍在分阶段迁移。全桌面触摸需使用签名且安装在受信任位置的 UIAccess 发行包，详见 [WinUI 3 与触摸架构说明](docs/WINUI3_TOUCH.md)。

<details open>
<summary><b>🎬 演示视频 / Video Demo </b></summary>
<br/>

<div align="center">
  <a href="[https://www.bilibili.com/video/BV1XjtA6KEGL](https://www.bilibili.com/video/BV1XjtA6KEGL)" target="_blank">
    <img src="./attachments/video_cover.png" width="700" alt="StarPie 演示视频" />
  </a>
  <p>
    <a href="https://www.bilibili.com/video/BV1XjtA6KEGL"><b>📺 点击前往 Bilibili 观看原声讲解与实机演示</b></a>
  </p>
</div>

</details>

---

## <a id="features"></a>✨ 功能特性

### 1. ⚡ 鼠标手势快速呼出与动作触发
- 按住鼠标右键滑动超过设定阈值即呼出轮盘，滑向目标扇区后松开按键即可触发对应动作（热键、打开程序、打开文件夹或系统功能）；
- 普通右键单击依然正常弹出原生右键菜单，互不冲突。

<div align="center">
  <img src="./attachments/第一张.gif" width="680" alt="鼠标手势快速呼出与动作触发演示" />
  <br/><br/>
  <img src="./attachments/第二张.gif" width="680" alt="实际场景下手势操作与动作执行演示" />
</div>

---

### 2. 🌟 多级级联子轮盘
- **多级轮盘级联交互**：支持在任意扇区方位自由扩展 1~4 个二级子动作。光标划向扇区并在扇区内停留时，外环以弹性动画平滑展开二级子扇区，向外滑入即可极速触发。
- **一二级主题与配色完全独立定制**：支持单独调节各级尺寸、字号与图标排版；二级轮盘既可**一键同步主轮盘**，也可**完全独立定制专属风格与配色**

<div align="center">
  <img src="./attachments/第三张.gif" width="680" alt="二级轮盘展示" />
</div>

---
### 3. 🚀 顺势外甩脱离取消 (Outer Escape Cancel)
- 若划出手势后不想执行任何动作，无需反向拉回中心核圆；
- 只需顺势向外快速滑动脱离轮盘边缘，轮盘自动进入半透明安全取消状态，松开右键不触发任何动作；
- 支持在设置中开启/关闭，并可通过滑块微调外甩距离灵敏度（140px ~ 320px）。

<div align="center">
  <img src="./attachments/02_outer_escape_cancel.gif" width="680" alt="顺势外甩脱离取消演示" />
</div>

---

### <a id="visuals"></a>4. 🎨 多种轮盘形态与风格预设
- **4 种几何形态**：经典紧凑扇区 (Original)、独立悬浮圆形 (Circle)、圆角胶囊 (Capsule)、蜂巢六边形 (HexagonHive)；
- **多套预设主题**：跟随系统、浅色模式、深色模式、液态毛玻璃、抹茶森林、冰川透蓝、莫兰迪柔灰；
- 右侧提供 **实时交互预览画布**，调节参数即时可见。

<div align="center">
  <img src="./attachments/03_themes_and_shapes.gif" width="680" alt="轮盘形态与主题风格切换演示" />
  <br/><br/>
  <img src="./attachments/03_1.gif" width="680" alt="多几何形态与视觉布局展示" />
  <br/><br/>
  <img src="./attachments/03_2.gif" width="680" alt="预设主题风格与画布实时渲染演示" />
</div>

---

### 5. 🎨 自定义高级配色与预设重命名
- 独立折叠面板，支持微调扇区底色、高亮光晕、边框线条与文字颜色；
- 支持十六进制颜色输入、色盘选取与屏幕吸色；
- 支持将当前颜色保存为自定义预设，并支持一键重命名与删除预设。

<div align="center">
  <img src="./attachments/04_custom_colors.gif" width="680" alt="自定义高级配色与预设管理演示" />
</div>

---

### 6. 🖼️ 自定义矢量 / 图片图标导入
- 图标库支持直接导入本地 **SVG 矢量文件** 与 **PNG / ICO / JPG** 图片；
- 导入图标自动保存在本地配置目录，支持在所有扇区中自由选用，并支持自定义图标重命名与删除。

<div align="center">
  <img src="./attachments/05_custom_icons.gif" width="680" alt="自定义图标导入与管理演示" />
</div>

---

### 7. 🎯 4 / 8 / 12 扇区方位自适应
- **4 键方位**：上下左右大角度，适合盲操；
- **8 键方位**：经典 8 向均衡布局（默认）；
- **12 键方位**：高密度功能映射，适合多动作工作流。

<div align="center">
  <img src="./attachments/06_sector_counts.gif" width="680" alt="4/8/12扇区分割自适应演示" />
</div>

---

### 8. 💼 多程序专属方案与应用快捷录入
- 支持针对 Chrome、VS Code、Photoshop、SolidWorks 等不同程序分别设置专属轮盘配置；
- 提供智能应用选择器，自动汇总已安装程序，支持快捷搜索过滤；
- 支持配置方案的新建、复制、删除与一键重命名。

<div align="center">
  <img src="./attachments/07_per_app_profiles.gif" width="680" alt="多程序专属方案演示" />
  <br/><br/>
  <img src="./attachments/07_1.gif" width="680" alt="应用程序智能检索与方案自适应切换" />
</div>

---

### 9. 🛡️ 场景隔离、全屏防误触与多语言
- **全屏与游戏检测**：运行全屏独占应用或游戏时自动放行原生右键；
- **修饰键穿透**：支持按住 Ctrl / Shift / Alt 时绕过轮盘；
- **黑名单支持**：支持将指定进程加入排除名单；
- **多语言热切换**：内置简体中文、繁体中文、English、日本語，切换即时生效。

<div align="center">
  <img src="./attachments/08_settings_and_i18n.gif" width="680" alt="防误触与多语言设置演示" />
</div>

---

## <a id="download"></a>🚀 快速开始与下载

### 最新稳定版：`v1.6.8` · WinUI 3 预览版：`v2.0.0-preview.2`

| 版本包 | 适用场景 | 说明 | 下载入口 |
| :--- | :--- | :--- | :--- |
| **独立免安装单文件版 (推荐)** | 所有用户 | 内置 .NET 运行时，解压即可运行 | [⬇️ 下载 StarPie.exe (Standalone)](https://github.com/SoftBlack42/StarPie/releases) |
| **轻量便携版** | 已安装 .NET 8 运行时的用户 | 体积小巧，绿色便携 | [⬇️ 下载 StarPie 便携包](https://github.com/SoftBlack42/StarPie/releases) |
| **历史版本归档** | 版本回溯与对比 | 历史版本的二进制文件与说明 | [📂 浏览 Releases 归档](https://github.com/SoftBlack42/StarPie/releases) |

### 基础使用流程：
1. 下载并运行 `StarPie.exe`，程序会在系统托盘中后台运行；
2. 在任意界面**按住鼠标右键划动**即可唤出轮盘；
3. 滑至目标扇区后**松开鼠标右键**触发对应操作；
4. 双击托盘图标或右键选择「偏好设置」，即可打开控制台进行详细配置。

---

## <a id="i18n"></a>🌐 多语言支持 (Internationalization)

可在设置页面的「⚙️ 高级与系统」中随时切换界面语言：

| 语言代码 | 显示名称 | 支持状态 |
| :--- | :--- | :---: |
| `zh-CN` | 🇨🇳 简体中文 | 🟢 完整支持 |
| `zh-TW` | 🇭🇰/🇹🇼 繁體中文 | 🟢 完整支持 |
| `en` | 🇺🇸 English | 🟢 完整支持 |
| `ja` | 🇯🇵 日本語 | 🟢 完整支持 |
| `Auto` | 🖥️ 跟随操作系统语言 | 🟢 完整支持 |

---

## <a id="build"></a>🛠️ 本地构建与开发

### 环境要求
- Windows 10 / 11 (x64 / ARM64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译与运行
```bash
# 1. 克隆代码仓库
git clone https://github.com/SoftBlack42/StarPie.git
cd StarPie

# 2. 编译 WinUI 3 主程序 (Release x64)
dotnet build WinPieGestures/WinPieGestures.csproj -c Release -p:Platform=x64

# 3. 运行项目
dotnet run --project WinPieGestures/WinPieGestures.csproj -p:Platform=x64

# 4. 如需构建 v1.6.8 WPF 兼容基线
dotnet build WinPieGestures/WinPieGestures.LegacyWpf.csproj -c Release
```

### 运行触摸核心测试 (5/5 通过 🟢)
```bash
dotnet run --project tests/StarPie.WinUI.InputTests/StarPie.WinUI.InputTests.csproj -c Release
```

---

## <a id="structure"></a>📂 项目结构

```
StarPie/
├── .github/                   # CI 工作流与社区配置文件
├── WinPieGestures/            # .NET 8 桌面应用
│   ├── WinPieGestures.csproj  # WinUI 3 主工程
│   ├── WinPieGestures.LegacyWpf.csproj # v1.6.8 兼容基线
│   └── WinUI/
│       ├── Controls/RadialMenuControl.cs # WinUI 扇区轮盘
│       ├── Input/             # 多指识别、全局触摸与回放
│       ├── Services/          # 主题、配置、执行、托盘
│       └── Views/             # WinUI 3 设置窗口与轮盘窗口
├── releases/                  # 版本发布归档目录
├── attachments/               # 功能演示动图与截图资源
├── tests/                     # 触摸状态机与旧版 GUI 回归测试
├── docs/WINUI3_TOUCH.md       # WinUI 3 / UIAccess 构建与触摸设计
├── CHANGELOG.md               # 版本更新日志
├── CONTRIBUTING.md            # 贡献指南
├── LICENSE                    # MIT 开源许可证
└── README.md                  # 主文档
```

---

## <a id="acknowledgements"></a>💡 开发故事与维护说明

### 🌟 灵感来源
本人是机械设计制造及其自动化专业的一名学生。在日常三维建模中经常使用 SolidWorks，觉得其内置的鼠标手势轮盘十分便利。

在接触到 AI Agent 辅助开发工具后，萌生了将这种轮盘操作迁移到 Windows 桌面全局的想法，希望能以此提升日常办公与操作的便利性。对于此前未接触过手势轮盘的朋友，这或许也是一种新颖高效的交互体验。

虽然开源社区已有同类轮盘项目，但在功能侧重点和交互细节上各有不同。从最初构想到发布，中途因学业课程与竞赛有所间断，与 AI Agent 协作断断续续历时约一周完成了当前版本。

项目中若有不够完善或考虑不周之处，还请多包涵。欢迎通过 GitHub Issue 提交 Bug 报告、使用反馈或改进建议！

### 🤖 人机协同开发说明
本项目由开发者主导架构设计、交互逻辑规划与系统调优，并由 AI 智能体（**AI Agent - Antigravity**）协同完成代码构建、多语言支持与 18 项 GUI 自动化测试验证。

### 📌 阶段性维护说明
- **当前状态**：v1.6.8 是 WPF 稳定基线；v2.0.0-preview.2 是 WinUI 3 与多指触摸迁移预览版；
- **后续节奏**：近期因学业与求职事务，版本更新将转为阶段性维护模式。欢迎社区伙伴提交 Pull Request 共同完善。

---

## <a id="license"></a>📄 开源许可证

本项目采用 [MIT License](LICENSE) 开源。
