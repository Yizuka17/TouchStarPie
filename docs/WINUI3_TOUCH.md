# StarPie v2 WinUI 3 与全局触摸架构

## 当前边界

`WinPieGestures/WinPieGestures.csproj` 是 v2 主入口，使用 .NET 8、Windows App SDK 和 WinUI 3。设置窗口、轮盘控件、透明悬浮窗、鼠标钩子、触摸识别、主题、托盘与动作执行均已进入 WinUI 3 运行时。

`WinPieGestures/WinPieGestures.LegacyWpf.csproj` 保留 v1.6.8 的完整 WPF 基线，目的是对照高级设置和逐页迁移，它不再是默认发布入口。

## 触摸手势语义

- 指数：单指、双指、三指可独立开关。
- 长按：默认 420ms，在长按完成前质心移动不得超过默认 18px 容差。
- 轮盘中心：使用所有活跃触点的质心，双指和三指不会被任意一根手指拉偏。
- 划动：默认 34px 进入方向选择，可选 4 向或 8 向；索引 0 为正上，之后顺时针增加。
- 触发：长按成功后松手执行当前方向动作；处于中心死区时松手取消。
- 放行：短点、提前划动或未启用的指数会通过 `InjectTouchInput` 回放给原应用。

## 输入流程

```text
RegisterPointerInputTarget(PT_TOUCH)
          │
          ▼
  message-only HWND
          │ POINTER down/update/up
          ▼
TouchGestureRecognizer ──未成手势──▶ TouchPassthroughInjector
          │
          └─长按成功──▶ WheelCoordinator ──▶ RadialMenuWindow
                                                     │
                                                     └─松手──▶ ActionExecutionService
```

Windows 一个桌面和指针类型同时只允许一个 `RegisterPointerInputTarget` 目标。如果其他辅助输入软件已占用该能力，StarPie 会在设置页显示失败原因并保持鼠标模式可用。

## UIAccess、签名与安装

Windows 对全桌面指针重定向有强制安全要求。仅在管理员启动一个未签名可执行文件并不能满足要求，正式发行必须：

1. 用 `-p:EnableUiAccess=true` 选择 `WinUI/app.uiaccess.manifest`。
2. 用可信任的 Authenticode 证书签名最终 EXE 及发行组件。
3. 安装到 `%ProgramFiles%` 或 `%WinDir%` 等 Windows 受信任位置。
4. 发布流水线启动后做真实触摸屏验收，确认单指点按回放、双指滚动与三指手势均不影响宿主应用。

开发构建默认使用 `uiAccess=false` 的 `WinUI/app.manifest`，可正常验证 UI、鼠标轮盘和纯状态机测试。它不会伪装全局触摸可用，开启时会显示可诊断警告。

### 本机长期测试证书

当前开发机已生成一张 10 年期的自签名代码签名证书，仅用于本机 UIAccess 测试：

- Subject：`CN=StarPie Local Code Signing, O=StarPie Studio`
- Thumbprint：`8676591D3E2C471458A6471F044AD7272FA31893`
- 有效期：2026-09-04 至 2036-09-04
- 算法：RSA 3072 + SHA-256，EKU 为 Code Signing
- 私钥备份：`%LOCALAPPDATA%\StarPie\Signing\StarPie-Local-CodeSigning-2026.pfx`
- 密码备份：同目录下 `*.password.dpapi.xml`，只有当前 Windows 用户在本机可解密
- 公钥证书：同目录下 `.cer`，已安装到 `CurrentUser\Root` 和 `CurrentUser\TrustedPublisher`

签名任意新的 StarPie 可执行文件：

```powershell
.\scripts\Sign-StarPie.ps1 .\path\to\StarPie.exe -EnableUiAccess
```

脚本固定选择上述指纹；`-EnableUiAccess` 会先在 Windows App SDK 生成的完整合并清单中安全改写并复验 `uiAccess=true`，再使用 SHA-256 Authenticode 签名和 RFC 3161 时间戳，最后调用 SignTool `/pa` 验证。如果个人证书库丢失证书，脚本会尝试从 PFX + DPAPI 备份恢复。

要安装已签名的 UIAccess 本机测试包，请在**管理员 PowerShell** 中执行：

```powershell
.\scripts\Install-StarPie-LocalUiAccess.ps1 `
  .\releases\v2.0.0-preview.2\SignedLocal\Expanded
```

安装脚本会先核验签名与固定指纹，再将公钥证书加入本机 `Root` / `TrustedPublisher`，并复制到 `%ProgramFiles%\StarPie`。它不会将私钥导入 LocalMachine。

**禁止将 PFX 或 DPAPI 密码备份提交到 Git、上传到发行页或发送给第三方。** `.gitignore` 已添加对这些私钥文件的全局忽略。这张自签名证书在其他电脑上默认不受信任，不得冒充面向公众的 CA 代码签名证书。

## 主题与轮盘材质

- `SystemThemeService` 根据 Windows 前景色判定深/浅色主题，并从 `UISettings` 取得系统强调色。
- 用户可选择跟随系统、强制浅色或强制深色；轮盘强调色也可关闭跟随。
- 悬浮轮盘支持 Acrylic 和 Solid；设置窗口内的实时预览使用实色替代背景材质，避免桌面 Backdrop 穿透预览卡片。
- 悬浮窗使用物理像素定位，设置窗口用 `DisplayArea` 与每窗口 DPI 缩放，不得回退到主屏逻辑像素假设。

## 构建与测试

```powershell
dotnet build .\WinPieGestures\WinPieGestures.csproj -c Release -p:Platform=x64
dotnet run --project .\tests\StarPie.WinUI.InputTests\StarPie.WinUI.InputTests.csproj -c Release

# WPF 对照基线
dotnet build .\WinPieGestures\WinPieGestures.LegacyWpf.csproj -c Release
```

独立非 MSIX 发布需同时设置 `WindowsAppSDKSelfContained=true`、`SelfContained=true`、`EnableMsixTooling=true`、`PublishSingleFile=true` 和 `IncludeAllContentForSelfExtract=true`。框架依赖版则将 `WindowsAppSDKSelfContained` 设为 `false`。本机 UIAccess 测试包例外：必须设置 `PublishSingleFile=false` 展开后再修改最终启动器资源；`mt.exe` 直接修改单文件会截断 .NET bundle 尾部载荷。
