using System.Runtime.InteropServices;
using WinPieGestures.WinUI.Services;

namespace WinPieGestures.WinUI.Input;

/// <summary>
/// Replays touch that did not become a StarPie long-press gesture. Input injected by the
/// process that owns the global redirection target is not redirected back to that process.
/// </summary>
internal sealed class TouchPassthroughInjector
{
    private readonly record struct SyntheticContact(uint Id, TouchPoint Point);

    private readonly Dictionary<uint, SyntheticContact> _syntheticContacts = [];
    private uint _nextSyntheticId = 1;
    private bool _initialized;

    public bool IsActive => _syntheticContacts.Count > 0;

    public bool Sync(IReadOnlyList<TouchContact> contacts)
    {
        if (!EnsureInitialized() || contacts.Count == 0)
        {
            return false;
        }

        NativeTouchMethods.PointerTouchInfo[] frame = new NativeTouchMethods.PointerTouchInfo[contacts.Count];
        bool needsCatchUpFrame = false;
        for (int index = 0; index < contacts.Count; index++)
        {
            TouchContact contact = contacts[index];
            bool isNew = !_syntheticContacts.TryGetValue(contact.Id, out SyntheticContact synthetic);
            if (isNew)
            {
                synthetic = new SyntheticContact(AllocateId(), contact.Start);
                needsCatchUpFrame |= contact.Start.DistanceTo(contact.Current) > 0.5;
            }
            synthetic = synthetic with { Point = contact.Current };
            _syntheticContacts[contact.Id] = synthetic;
            frame[index] = CreateContact(
                synthetic.Id,
                isNew ? contact.Start : contact.Current,
                isNew
                    ? NativeTouchMethods.POINTER_FLAG_DOWN |
                      NativeTouchMethods.POINTER_FLAG_INRANGE | NativeTouchMethods.POINTER_FLAG_INCONTACT
                    : NativeTouchMethods.POINTER_FLAG_UPDATE | NativeTouchMethods.POINTER_FLAG_INRANGE |
                      NativeTouchMethods.POINTER_FLAG_INCONTACT,
                index == 0);
        }
        if (!Inject(frame))
        {
            return false;
        }

        // A catch-up frame is retained as a safety net for a contact observed midway
        // through a sequence. The normal service path mirrors every frame immediately.
        if (needsCatchUpFrame)
        {
            for (int index = 0; index < contacts.Count; index++)
            {
                TouchContact contact = contacts[index];
                SyntheticContact synthetic = _syntheticContacts[contact.Id];
                frame[index] = CreateContact(
                    synthetic.Id,
                    contact.Current,
                    NativeTouchMethods.POINTER_FLAG_UPDATE | NativeTouchMethods.POINTER_FLAG_INRANGE |
                    NativeTouchMethods.POINTER_FLAG_INCONTACT,
                    index == 0);
            }
            return Inject(frame);
        }
        return true;
    }

    public bool EndContact(uint physicalId, TouchPoint point, IReadOnlyList<TouchContact> remaining)
    {
        if (!_syntheticContacts.TryGetValue(physicalId, out SyntheticContact ending))
        {
            return false;
        }

        NativeTouchMethods.PointerTouchInfo[] frame = new NativeTouchMethods.PointerTouchInfo[remaining.Count + 1];
        int index = 0;
        foreach (TouchContact contact in remaining)
        {
            if (_syntheticContacts.TryGetValue(contact.Id, out SyntheticContact active))
            {
                frame[index++] = CreateContact(
                    active.Id,
                    contact.Current,
                    NativeTouchMethods.POINTER_FLAG_UPDATE | NativeTouchMethods.POINTER_FLAG_INRANGE |
                      NativeTouchMethods.POINTER_FLAG_INCONTACT,
                    index == 1);
                _syntheticContacts[contact.Id] = active with { Point = contact.Current };
            }
        }
        frame[index++] = CreateContact(ending.Id, point, NativeTouchMethods.POINTER_FLAG_UP, index == 1);

        if (index != frame.Length)
        {
            Array.Resize(ref frame, index);
        }
        bool result = Inject(frame);
        _syntheticContacts.Remove(physicalId);
        return result;
    }

    public void CancelAll(bool canceled = true)
    {
        if (_syntheticContacts.Count == 0)
        {
            return;
        }

        NativeTouchMethods.PointerTouchInfo[] frame = _syntheticContacts.Values
            .Select((contact, index) => CreateContact(
                contact.Id,
                contact.Point,
                NativeTouchMethods.POINTER_FLAG_UP |
                (canceled ? NativeTouchMethods.POINTER_FLAG_CANCELED : 0),
                index == 0))
            .ToArray();
        Inject(frame);
        _syntheticContacts.Clear();
    }

    public void Reset() => _syntheticContacts.Clear();

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return true;
        }
        _initialized = NativeTouchMethods.InitializeTouchInjection(10, NativeTouchMethods.TOUCH_FEEDBACK_DEFAULT);
        if (!_initialized)
        {
            AppLog.Error($"InitializeTouchInjection failed: {Marshal.GetLastWin32Error()}");
        }
        return _initialized;
    }

    private uint AllocateId()
    {
        uint id = _nextSyntheticId++;
        if (_nextSyntheticId > 250)
        {
            _nextSyntheticId = 1;
        }
        return id;
    }

    private static NativeTouchMethods.PointerTouchInfo CreateContact(
        uint pointerId,
        TouchPoint point,
        uint flags,
        bool primary)
    {
        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);
        const int radius = 2;
        if (primary)
        {
            flags |= NativeTouchMethods.POINTER_FLAG_PRIMARY;
        }

        return new NativeTouchMethods.PointerTouchInfo
        {
            PointerInfo = new NativeTouchMethods.PointerInfo
            {
                PointerType = NativeTouchMethods.PT_TOUCH,
                PointerId = pointerId,
                PointerFlags = flags,
                PixelLocation = new NativeTouchMethods.NativePoint { X = x, Y = y },
                PixelLocationRaw = new NativeTouchMethods.NativePoint { X = x, Y = y }
            },
            TouchMask = NativeTouchMethods.TOUCH_MASK_CONTACTAREA |
                        NativeTouchMethods.TOUCH_MASK_ORIENTATION |
                        NativeTouchMethods.TOUCH_MASK_PRESSURE,
            Contact = new NativeTouchMethods.NativeRect
            {
                Left = x - radius,
                Top = y - radius,
                Right = x + radius,
                Bottom = y + radius
            },
            ContactRaw = new NativeTouchMethods.NativeRect
            {
                Left = x - radius,
                Top = y - radius,
                Right = x + radius,
                Bottom = y + radius
            },
            Orientation = 90,
            Pressure = 32000
        };
    }

    private static bool Inject(NativeTouchMethods.PointerTouchInfo[] frame)
    {
        bool result = NativeTouchMethods.InjectTouchInput((uint)frame.Length, frame);
        if (!result)
        {
            AppLog.Error($"InjectTouchInput failed: {Marshal.GetLastWin32Error()}");
        }
        return result;
    }
}
