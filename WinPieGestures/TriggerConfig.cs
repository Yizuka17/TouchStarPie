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
/// Global touch trigger settings. Global redirection is opt-in because Windows requires
/// a signed UIAccess build and redirects the selected pointer type to StarPie.
/// </summary>
public sealed class TouchTriggerConfig
{
	public bool Enabled { get; set; }

	public bool EnableOneFinger { get; set; } = true;

	public bool EnableTwoFinger { get; set; } = true;

	public bool EnableThreeFinger { get; set; } = true;

	/// <summary>Duration that all contacts must remain within the movement tolerance.</summary>
	public double LongPressDelayMs { get; set; } = 420.0;

	/// <summary>Maximum pre-activation movement in physical pixels.</summary>
	public double HoldMovementTolerance { get; set; } = 18.0;

	/// <summary>Distance from the activation centroid before a direction is selected.</summary>
	public double SwipeThreshold { get; set; } = 34.0;

	/// <summary>Number of direction slices used by touch gestures: 4 or 8.</summary>
	public int DirectionCount { get; set; } = 8;

	/// <summary>Keep unhandled touch usable by reinjecting it into the original desktop target.</summary>
	public bool PassThroughUnhandledTouch { get; set; } = true;
}
