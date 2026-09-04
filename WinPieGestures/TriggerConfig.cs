namespace WinPieGestures;

public class TriggerConfig
{
	public string TriggerType { get; set; } = "Mouse";

	public string MouseButton { get; set; } = "RightButton";

	public string Key { get; set; } = "None";

	public uint VkCode { get; set; }

	public bool RequireCtrl { get; set; }

	public bool RequireShift { get; set; }

	public bool RequireAlt { get; set; }

	public bool RequireWin { get; set; }

	public string DisplayText { get; set; } = "\ud83d\uddb1\ufe0f 鼠标右键 (Right Button)";
}

/// <summary>
/// Passive global touchscreen trigger settings. StarPie observes raw touchscreen HID reports
/// in parallel with the normal Windows pointer pipeline and never redirects or reinjects touch.
/// </summary>
public sealed class TouchTriggerConfig
{
	public bool Enabled { get; set; }

	public bool EnableOneFinger { get; set; } = true;

	public bool EnableTwoFinger { get; set; } = true;

	public bool EnableThreeFinger { get; set; } = true;

	/// <summary>Duration that the complete finger chord must remain stable before it is armed.</summary>
	public double LongPressDelayMs { get; set; } = 420.0;

	/// <summary>Maximum movement per contact while waiting for the long-press arm.</summary>
	public double HoldMovementTolerance { get; set; } = 18.0;

	/// <summary>Centroid movement after arming required to invoke the wheel and select a direction.</summary>
	public double SwipeThreshold { get; set; } = 34.0;

	/// <summary>Number of direction slices used by touch gestures: 4 or 8.</summary>
	public int DirectionCount { get; set; } = 8;

	/// <summary>
	/// Legacy v2-preview compatibility flag. Passive Raw Input leaves native touch untouched, so
	/// there is no passthrough/injection mode to enable or disable anymore.
	/// </summary>
	public bool PassThroughUnhandledTouch { get; set; } = true;
}
