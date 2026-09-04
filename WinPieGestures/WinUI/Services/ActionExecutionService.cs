using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPieGestures.WinUI.Services;

public sealed class ActionExecutionService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint MapVkToScanCode = 0;

    public async Task ExecuteAsync(ActionItem action)
    {
        if (action is null || string.Equals(action.Type, "None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            switch (action.Type.ToLowerInvariant())
            {
                case "hotkey":
                    await SendHotkeyAsync(action.Parameter);
                    break;
                case "launch":
                    StartProcess(action.Parameter, action.Arguments);
                    break;
                case "weburl":
                    StartProcess(action.Parameter, string.Empty);
                    break;
                case "command":
                    StartCommand(action);
                    break;
                case "system":
                    await ExecuteSystemActionAsync(action.Parameter);
                    break;
                default:
                    AppLog.Info($"Unsupported WinUI action type: {action.Type}");
                    break;
            }
        }
        catch (Exception exception)
        {
            AppLog.Error($"Action failed: {action.Name}", exception);
        }
    }

    private static async Task SendHotkeyAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return;
        }

        foreach (string chord in expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] tokens = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<ushort> modifiers = [];
            ushort mainKey = 0;
            foreach (string token in tokens)
            {
                ushort virtualKey = VirtualKeyFromName(token);
                if (virtualKey == 0)
                {
                    continue;
                }
                if (virtualKey is 0x10 or 0x11 or 0x12 or 0x5B)
                {
                    modifiers.Add(virtualKey);
                }
                else
                {
                    mainKey = virtualKey;
                }
            }

            foreach (ushort modifier in modifiers)
            {
                SendKey(modifier, false);
            }
            if (modifiers.Count > 0)
            {
                await Task.Delay(12);
            }
            if (mainKey != 0)
            {
                SendKey(mainKey, false);
                SendKey(mainKey, true);
            }
            for (int index = modifiers.Count - 1; index >= 0; index--)
            {
                SendKey(modifiers[index], true);
            }
            await Task.Delay(24);
        }
    }

    private static async Task ExecuteSystemActionAsync(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "lock":
                LockWorkStation();
                break;
            case "showdesktop":
                await SendHotkeyAsync("Win+D");
                break;
            case "screenshot":
                SendKey(0x2C, false);
                SendKey(0x2C, true);
                break;
            case "volumeup":
                SendKey(0xAF, false);
                SendKey(0xAF, true);
                break;
            case "volumedown":
                SendKey(0xAE, false);
                SendKey(0xAE, true);
                break;
            case "volumemute":
                SendKey(0xAD, false);
                SendKey(0xAD, true);
                break;
            case "taskmanager":
                StartProcess("taskmgr.exe", string.Empty);
                break;
            case "calculator":
                StartProcess("calculator:", string.Empty);
                break;
        }
    }

    private static void StartProcess(string fileName, string arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        });
    }

    private static void StartCommand(ActionItem action)
    {
        string terminal = action.CommandTerminal?.ToLowerInvariant() ?? "cmd";
        ProcessStartInfo info = terminal switch
        {
            "powershell" => new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{action.Parameter}\""),
            "wsl" => new ProcessStartInfo("wsl.exe", $"-- {action.Parameter}"),
            "direct" => new ProcessStartInfo(action.Parameter, action.Arguments),
            _ => new ProcessStartInfo("cmd.exe", $"/C {action.Parameter}")
        };
        info.UseShellExecute = true;
        Process.Start(info);
    }

    private static void SendKey(ushort virtualKey, bool keyUp)
    {
        ushort scanCode = (ushort)MapVirtualKey(virtualKey, MapVkToScanCode);
        uint flags = KeyEventScanCode | (keyUp ? KeyEventKeyUp : 0);
        if (IsExtendedKey(virtualKey))
        {
            flags |= KeyEventExtendedKey;
        }
        Input[] inputs =
        [
            new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = 0,
                        ScanCode = scanCode,
                        Flags = flags
                    }
                }
            }
        ];
        SendInput(1, inputs, Marshal.SizeOf<Input>());
    }

    private static bool IsExtendedKey(ushort key) => key is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or
        0x2D or 0x2E or 0x5B or 0x5C or 0x6F or 0x90 or 0x91;

    private static ushort VirtualKeyFromName(string name)
    {
        string key = name.Trim().ToUpperInvariant();
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return key[0];
        }
        if (key.StartsWith('F') && int.TryParse(key[1..], out int fNumber) && fNumber is >= 1 and <= 24)
        {
            return (ushort)(0x70 + fNumber - 1);
        }
        return key switch
        {
            "CTRL" or "CONTROL" => 0x11,
            "SHIFT" => 0x10,
            "ALT" => 0x12,
            "WIN" or "WINDOWS" => 0x5B,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "END" => 0x23,
            "HOME" => 0x24,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "INSERT" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            "PRINTSCREEN" or "PRTSC" => 0x2C,
            "PAUSE" => 0x13,
            "BACKSPACE" => 0x08,
            "`" => 0xC0,
            _ => 0
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();
}
