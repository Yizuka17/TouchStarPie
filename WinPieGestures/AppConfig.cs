using System.Collections.Generic;

namespace WinPieGestures;

public class AppConfig
{
	public string Language { get; set; } = "Auto";

	public string TriggerButton { get; set; } = "RightButton";

	public TriggerConfig Trigger { get; set; } = new TriggerConfig();

	/// <summary>Single-, two-, and three-finger long-press plus directional swipe trigger.</summary>
	public TouchTriggerConfig TouchTrigger { get; set; } = new TouchTriggerConfig();

	public double DragThreshold { get; set; } = 25.0;

	/// <summary>核心圆死区唤醒灵敏度（有效判定半径，像素）。光标在此半径内视为停留在中心核圆，可触发中心动作或静默取消。</summary>
	public double CoreDeadzoneRadius { get; set; } = 35.0;

	/// <summary>可选：长按触发按键（如右键）不动达到长按阈值后呼出轮盘，与拖动呼出共存。</summary>
	public bool LongPressTrigger { get; set; }

	/// <summary>长按响应时长（毫秒）。</summary>
	public double LongPressDelayMs { get; set; } = 450.0;

	// ---- 鼠标手势（画轨迹识别，最多三段图样）----
	public bool GestureEnabled { get; set; }

	/// <summary>手势触发键："RightButton"/"MiddleButton"/"XButton1"/"XButton2"。</summary>
	public string GestureTriggerButton { get; set; } = "MiddleButton";

	public List<GestureMapping> GestureMappings { get; set; } = new List<GestureMapping>();

	/// <summary>手势提示文字位置："Auto" 或 U/D/L/R/UL/UR/DL/DR（相对鼠标）。</summary>
	public string GestureHintPlacement { get; set; } = "Auto";

	/// <summary>手势段灵敏度：最小段长（像素）。越大越难把中途小拐弯误识别为方向段。</summary>
	public double GestureSegmentSensitivity { get; set; } = 16.0;

	/// <summary>取消（回到轮盘中心松手且未选中任何动作）后执行的自定义动作；默认关闭=仅关面板不执行。</summary>
	public bool EnableCancelAction { get; set; }

	public ActionItem CancelAction { get; set; } = new ActionItem { Type = "Hotkey", Name = "取消动作", Parameter = "" };

	/// <summary>平铺排除名单：进程 exe 名（不含扩展名），逗号/分号分隔。</summary>
	public string TileExcludeProcesses { get; set; } = "";

	/// <summary>平铺是否包含最小化窗口（true=还原后参与平铺）。</summary>
	public bool TileIncludeMinimized { get; set; }

	/// <summary>平铺"循环切换"参与范围：布局 key 逗号分隔（空=全部布局参与循环）。</summary>
	public string TileCycleLayouts { get; set; } = "";

	public string AnimationSpeed { get; set; } = "Balanced";

	public double CustomAnimationDurationMs { get; set; } = 80.0;

	public bool EnableOuterEscapeCancel { get; set; }

	public double OuterEscapeDistance { get; set; } = 186.0;

	public string AppTheme { get; set; } = "System";

	/// <summary>Use the current Windows accent color for selection and focus feedback.</summary>
	public bool UseSystemAccentColor { get; set; } = true;

	/// <summary>Fluent wheel material: "Acrylic" or "Solid".</summary>
	public string WheelMaterial { get; set; } = "Acrylic";

	public string Theme { get; set; } = "System";

	public string UiStyle { get; set; } = "ClassicRing";
	public string SubmenuStyle { get; set; } = "Wheel";

	public bool EnableMultiTier { get; set; } = true;

	/// <summary>在设置控制台拖拽对调一级扇区时，是否连同其绑定的二级级联子动作一块换位（默认 true）。</summary>
	public bool LinkSubActionsWhenDragging { get; set; } = true;

	public double SubWheelRadiusRatio { get; set; } = 1.55;

	public double SubWheelTriggerDistance { get; set; } = 95.0;

	public double SubWheelOuterRadius { get; set; } = 210.0;

	public double SubWheelInnerGap { get; set; } = 4.0;

	public double SubWheelCornerRadius { get; set; } = 4.0;

	public double SubWheelIconSize { get; set; } = 18.0;

	public double SubWheelFontSize { get; set; } = 9.5;

	public bool UseIndependentSubWheelTheme { get; set; }

	public string SubWheelUiStyle { get; set; } = "ClassicRing";

	public string SubWheelTheme { get; set; } = "FollowPrimary";

	public string SubWheelCustomSectorBg { get; set; } = "#9016161A";

	public string SubWheelCustomSectorBorder { get; set; } = "#35FFFFFF";

	public string SubWheelCustomHighlightBg { get; set; } = "#E06C4DFF";

	public string SubWheelCustomHighlightBorder { get; set; } = "#A0FFFFFF";

	public string SubWheelCustomText { get; set; } = "#E0FFFFFF";

	public string SubWheelHighlightGlowPreset { get; set; } = "FollowPrimary";

	public string SubWheelHighlightGlowColor { get; set; } = "";

	public double SubWheelHighlightGlowRadius { get; set; } = 24.0;

	public double SubWheelHighlightGlowOpacity { get; set; } = 0.85;

	public bool AutoStartAsAdmin { get; set; }

	public bool ShowText { get; set; } = true;

	public bool ShowSelectedActionText { get; set; }

	public double WheelRadius { get; set; } = 138.0;

	public double InnerRadius { get; set; } = 52.0;

	public double CoreRadius { get; set; } = 50.0;

	public string Shape { get; set; } = "Original";

	public double SectorGap { get; set; } = 2.0;

	public double SectorCornerRadius { get; set; } = 4.0;

	public string IconLayoutMode { get; set; } = "IconAndText";

	public string SectorTextPlacement { get; set; } = "Below";

	public double SectorTextOffsetX { get; set; } = 0.0;

	public double SectorTextOffsetY { get; set; } = 0.0;

	public string WheelFontFamily { get; set; } = "Microsoft YaHei UI, Segoe UI";

	public double SectorIconSize { get; set; } = 20.0;

	public double SectorFontSize { get; set; } = 11.0;

	public string CoreFontFamily { get; set; } = "Microsoft YaHei UI, Segoe UI";

	public double CoreFontSize { get; set; } = 13.0;

	public string CoreTextColor { get; set; } = "#FFFFFFFF";

	public string CoreTitle { get; set; } = "StarPie";

	public string CoreSubtitle { get; set; } = "RMB Drag";

	public bool ShowCoreIcon { get; set; } = true;

	public string CoreIconType { get; set; } = "Exit";

	public string CoreCustomIconKey { get; set; } = "";

	public string CoreCustomIconSvg { get; set; } = "";

	public string CoreCustomImagePath { get; set; } = "";

	public string CoreCustomImageStretch { get; set; } = "UniformToFill";

	public double CoreIconScale { get; set; } = 1.0;

	public double CoreImageOffsetX { get; set; }

	public double CoreImageOffsetY { get; set; }

	public string HighlightGlowPreset { get; set; } = "Auto";

	public string HighlightGlowColor { get; set; } = "";

	public double HighlightGlowRadius { get; set; } = 24.0;

	public double HighlightGlowOpacity { get; set; } = 0.85;

	public string CustomSectorBg { get; set; } = "#9016161A";

	public string CustomSectorBorder { get; set; } = "#35FFFFFF";

	public string CustomHighlightBg { get; set; } = "#E06C4DFF";

	public string CustomHighlightBorder { get; set; } = "#A0FFFFFF";

	public string CustomText { get; set; } = "#E0FFFFFF";

	public string CustomCoreBg { get; set; } = "#F20F172A";

	public List<CustomColorPreset> CustomColorPresets { get; set; } = new List<CustomColorPreset>();

	public string WheelBgImagePath { get; set; } = "";

	public double WheelBgOpacity { get; set; } = 0.8;

	public string WheelBgStretch { get; set; } = "UniformToFill";

	public string CoreBgImagePath { get; set; } = "";

	public double CoreBgOpacity { get; set; } = 1.0;

	public string CoreBgStretch { get; set; } = "UniformToFill";

	public string HighlightTexturePath { get; set; } = "";

	public double HighlightTextureOpacity { get; set; } = 0.7;

	public List<WheelProfile> Profiles { get; set; } = new List<WheelProfile>();

	public string IsolationMode { get; set; } = "Blacklist";

	public List<string> BlacklistedProcesses { get; set; } = new List<string> { "mstsc.exe", "paint.exe" };

	public List<string> WhitelistedProcesses { get; set; } = new List<string>();

	public bool DisableOnCtrl { get; set; }

	public bool DisableOnShift { get; set; }

	public bool DisableOnAlt { get; set; }

	public bool DisableOnFullScreen { get; set; } = true;

	public bool AutoCheckUpdate { get; set; } = true;

	public string UpdateChannel { get; set; } = "Stable";

	public string UpdateProxySource { get; set; } = "ghproxy";

	public string CustomProxyUrl { get; set; } = "";

	public string LastCheckUpdateTime { get; set; } = "";

	public string IgnoredVersion { get; set; } = "";

	/// <summary>平铺窗口高级设置卡片是否展开（默认收起折叠）</summary>
	public bool TileSettingsExpanded { get; set; } = false;

	/// <summary>是否启用屏幕边缘呼出智能防溢出与光标自动对齐（默认开启）</summary>
	public bool EnableEdgeCollisionAvoidance { get; set; } = true;

	/// <summary>屏幕边缘呼出防溢出策略："ClampShift" (智能贴边安全防溢出 - 默认), "ScreenCenter" (屏幕中心呼出), "None" (原生不处理)</summary>
	public string EdgeOverflowPolicy { get; set; } = "ClampShift";

	/// <summary>屏幕边缘呼出 X 轴水平安全边距 (像素，默认 16px)</summary>
	public double EdgeSafeMarginX { get; set; } = 16.0;

	/// <summary>屏幕边缘呼出 Y 轴垂直安全边距 (像素，默认 16px)</summary>
	public double EdgeSafeMarginY { get; set; } = 16.0;

	/// <summary>屏幕边缘呼出安全边距 (像素，保留兼容)</summary>
	public double EdgeSafeMargin { get; set; } = 16.0;

	/// <summary>OCR 截屏文字识别引擎全局配置</summary>
	public OcrSettings OcrConfig { get; set; } = new OcrSettings();
}

public class OcrSettings
{
	/// <summary>OCR 服务提供商："Local" (Windows原生离线), "Ai" (OpenAI兼容多模态), "Cloud" (商业云端), "Custom" (自定义HTTP微服务)</summary>
	public string Provider { get; set; } = "Local";

	/// <summary>本地 OCR 首选语言（如 "zh-Hans", "zh-Hant", "en-US", "ja-JP"）</summary>
	public string LocalLanguage { get; set; } = "zh-Hans";

	/// <summary>AI 多模态大模型接口端点 (如 "https://api.openai.com/v1")</summary>
	public string AiEndpoint { get; set; } = "https://api.openai.com/v1";

	/// <summary>AI 多模态大模型密钥 (sk-...)</summary>
	public string AiApiKey { get; set; } = "";

	/// <summary>AI 模型名称 (如 "gpt-4o-mini", "Qwen/Qwen2.5-VL-72B-Instruct")</summary>
	public string AiModel { get; set; } = "gpt-4o-mini";

	/// <summary>AI 识别输出模式："text" (纯文字提取), "latex" (LaTeX公式), "markdown" (Markdown表格), "translate" (自动译为中文)</summary>
	public string AiPromptMode { get; set; } = "text";

	/// <summary>商业云端 OCR 服务商："Baidu", "Tencent", "Aliyun"</summary>
	public string CloudProvider { get; set; } = "Baidu";

	public string CloudApiKey { get; set; } = "";

	public string CloudSecretKey { get; set; } = "";

	/// <summary>自定义私有化 HTTP OCR 微服务接口 URL (如 "http://127.0.0.1:1224/api/ocr")</summary>
	public string CustomHttpUrl { get; set; } = "http://127.0.0.1:1224/api/ocr";

	public string CustomHttpFormat { get; set; } = "base64";

	/// <summary>识别完成后是否自动复制到系统剪贴板</summary>
	public bool AutoCopyToClipboard { get; set; } = true;

	/// <summary>识别完成后是否弹出结果悬浮窗口</summary>
	public bool ShowResultWindow { get; set; } = true;

	/// <summary>识别完成后是否直接在默认浏览器中搜索</summary>
	public bool SearchInBrowser { get; set; } = false;

	/// <summary>自动合并断句段落</summary>
	public bool MergeLines { get; set; } = true;

	/// <summary>自动去除中文字符间多余空格</summary>
	public bool RemoveSpacesBetweenCjk { get; set; } = true;
}

