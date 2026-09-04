namespace WinPieGestures.WinUI.Services;

/// <summary>
/// Central icon mapping shared by the WinUI preview and the desktop layered wheel.
/// Uses Segoe MDL2 Assets because the glyph code points are stable across Windows 10/11.
/// </summary>
internal static class ActionIconCatalog
{
    public const string FontFamilyName = "Segoe MDL2 Assets";

    public static string Resolve(ActionItem? action)
    {
        if (action is null)
        {
            return "\uE945";
        }

        string key = action.IconKey?.Trim().ToLowerInvariant() ?? string.Empty;
        string parameter = action.Parameter?.Trim().ToLowerInvariant() ?? string.Empty;
        string type = action.Type?.Trim().ToLowerInvariant() ?? string.Empty;

        return key switch
        {
            "copy" => "\uE8C8",
            "paste" => "\uE77F",
            "cut" => "\uE8C6",
            "undo" => "\uE7A7",
            "redo" => "\uE7A6",
            "search" => "\uE721",
            "save" => "\uE74E",
            "lock" => "\uE72E",
            "screenshot" or "screenclip" or "snip" => "\uE722",
            "volumeup" => "\uE995",
            "volumedown" => "\uE993",
            "mute" or "volumemute" => "\uE74F",
            "showdesktop" or "desktop" => "\uE977",
            "terminal" or "command" or "cmd" or "powershell" => "\uE756",
            "code" => "\uE943",
            "calculator" => "\uE8EF",
            "close" or "closewindow" => "\uE8BB",
            "minimize" => "\uE921",
            "maximize" => "\uE922",
            "restore" => "\uE923",
            "snapleft" => "\uE90C",
            "snapright" => "\uE90D",
            "taskview" => "\uE7C4",
            "prevdesktop" or "previousdesktop" => "\uE973",
            "nextdesktop" => "\uE974",
            "folder" => "\uE8B7",
            "open" or "openfile" or "launch" => "\uE8E5",
            "browser" or "web" or "website" or "globe" => "\uE774",
            "settings" or "setting" => "\uE713",
            "keyboard" or "hotkey" => "\uE765",
            "system" => "\uE770",
            "tile" => "\uE902",
            "switchwindow" or "switchapps" => "\uE8F9",
            "back" => "\uE72B",
            "forward" => "\uE72A",
            "refresh" => "\uE72C",
            "delete" => "\uE74D",
            "print" => "\uE749",
            "none" => "\uE711",
            _ => ResolveFromAction(type, parameter)
        };
    }

    private static string ResolveFromAction(string type, string parameter)
    {
        if (parameter.Contains("volumeup")) return "\uE995";
        if (parameter.Contains("volumedown")) return "\uE993";
        if (parameter.Contains("mute")) return "\uE74F";
        if (parameter.Contains("showdesktop")) return "\uE977";
        if (parameter.Contains("screenshot") || parameter.Contains("screenclip")) return "\uE722";
        if (parameter.Contains("lock")) return "\uE72E";
        if (parameter.Contains("taskview")) return "\uE7C4";
        if (parameter.Contains("calculator")) return "\uE8EF";

        return type switch
        {
            "hotkey" => "\uE765",
            "launch" => "\uE8E5",
            "weburl" => "\uE774",
            "command" => "\uE756",
            "system" => "\uE770",
            "none" => "\uE711",
            _ => "\uE945"
        };
    }
}
