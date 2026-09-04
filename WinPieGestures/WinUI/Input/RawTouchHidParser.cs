using System.Runtime.InteropServices;

namespace WinPieGestures.WinUI.Input;

internal readonly record struct RawTouchContact(uint Id, TouchPoint Point);

internal readonly record struct RawTouchFrame(nint Device, IReadOnlyList<RawTouchContact> Contacts);

/// <summary>
/// Decodes raw HID reports from a UsagePage=Digitizer / Usage=TouchScreen top-level
/// collection. The parser is deliberately read-only: it never redirects pointer routing and
/// never injects replacement contacts.
/// </summary>
internal sealed class RawTouchHidParser : IDisposable
{
    private readonly Dictionary<nint, DeviceDescriptor> _devices = [];

    public bool TryParse(nint rawInputHandle, out RawTouchFrame frame)
    {
        frame = default;
        uint headerSize = (uint)Marshal.SizeOf<NativeTouchMethods.RawInputHeader>();
        uint bufferSize = 0;
        uint probe = NativeTouchMethods.GetRawInputData(
            rawInputHandle,
            NativeTouchMethods.RID_INPUT,
            0,
            ref bufferSize,
            headerSize);
        if (probe == uint.MaxValue || bufferSize < headerSize + 8)
        {
            return false;
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            uint size = bufferSize;
            uint copied = NativeTouchMethods.GetRawInputData(
                rawInputHandle,
                NativeTouchMethods.RID_INPUT,
                buffer,
                ref size,
                headerSize);
            if (copied == uint.MaxValue || copied < headerSize + 8)
            {
                return false;
            }

            NativeTouchMethods.RawInputHeader header = Marshal.PtrToStructure<NativeTouchMethods.RawInputHeader>(buffer);
            if (header.Type != NativeTouchMethods.RIM_TYPEHID || header.Device == 0)
            {
                return false;
            }

            uint reportLength = ReadUInt32(buffer, checked((int)headerSize));
            uint reportCount = ReadUInt32(buffer, checked((int)headerSize + 4));
            if (reportLength == 0 || reportCount == 0)
            {
                frame = new RawTouchFrame(header.Device, []);
                return true;
            }

            ulong required = (ulong)headerSize + 8UL + (ulong)reportLength * reportCount;
            if (required > copied)
            {
                return false;
            }

            DeviceDescriptor descriptor = GetOrCreateDevice(header.Device);
            Dictionary<uint, RawTouchContact> contacts = [];
            uint? expectedContactCount = null;
            nint reports = buffer + checked((int)headerSize + 8);
            for (uint index = 0; index < reportCount; index++)
            {
                nint report = reports + checked((int)(index * reportLength));
                descriptor.AppendReport(report, reportLength, contacts, ref expectedContactCount);
                if (expectedContactCount == 0)
                {
                    contacts.Clear();
                    break;
                }
            }

            IReadOnlyList<RawTouchContact> result = contacts.Values.ToArray();
            if (expectedContactCount is uint expected && result.Count > expected)
            {
                result = result.Take(checked((int)expected)).ToArray();
            }
            frame = new RawTouchFrame(header.Device, result);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void ForgetDevice(nint device)
    {
        if (_devices.Remove(device, out DeviceDescriptor? descriptor))
        {
            descriptor.Dispose();
        }
    }

    public void Reset()
    {
        foreach (DeviceDescriptor descriptor in _devices.Values)
        {
            descriptor.Dispose();
        }
        _devices.Clear();
    }

    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }

    private DeviceDescriptor GetOrCreateDevice(nint device)
    {
        if (_devices.TryGetValue(device, out DeviceDescriptor? descriptor))
        {
            return descriptor;
        }
        descriptor = DeviceDescriptor.Create(device);
        _devices[device] = descriptor;
        return descriptor;
    }

    private static uint ReadUInt32(nint pointer, int offset) => unchecked((uint)Marshal.ReadInt32(pointer, offset));

    private sealed class DeviceDescriptor : IDisposable
    {
        private readonly nint _preparsedData;
        private readonly IReadOnlyList<ContactLayout> _contacts;
        private readonly ushort? _contactCountLink;

        private DeviceDescriptor(
            nint preparsedData,
            IReadOnlyList<ContactLayout> contacts,
            ushort? contactCountLink)
        {
            _preparsedData = preparsedData;
            _contacts = contacts;
            _contactCountLink = contactCountLink;
        }

        public static DeviceDescriptor Create(nint device)
        {
            uint preparsedSize = 0;
            uint probe = NativeTouchMethods.GetRawInputDeviceInfo(
                device,
                NativeTouchMethods.RIDI_PREPARSEDDATA,
                0,
                ref preparsedSize);
            if (probe == uint.MaxValue || preparsedSize == 0)
            {
                throw new InvalidOperationException("Unable to query touchscreen HID preparsed-data size.");
            }

            nint preparsed = Marshal.AllocHGlobal(checked((int)preparsedSize));
            try
            {
                uint actualSize = preparsedSize;
                uint result = NativeTouchMethods.GetRawInputDeviceInfo(
                    device,
                    NativeTouchMethods.RIDI_PREPARSEDDATA,
                    preparsed,
                    ref actualSize);
                if (result == uint.MaxValue || actualSize == 0)
                {
                    throw new InvalidOperationException("Unable to read touchscreen HID preparsed data.");
                }

                nint caps = Marshal.AllocHGlobal(NativeTouchMethods.HIDP_CAPS_SIZE);
                try
                {
                    if (NativeTouchMethods.HidP_GetCaps(preparsed, caps) != NativeTouchMethods.HIDP_STATUS_SUCCESS)
                    {
                        throw new InvalidOperationException("HidP_GetCaps failed for touchscreen device.");
                    }

                    ushort valueCapsCount = ReadUInt16(
                        caps,
                        NativeTouchMethods.HIDP_CAPS_NUMBER_INPUT_VALUE_CAPS_OFFSET);
                    if (valueCapsCount == 0)
                    {
                        throw new InvalidOperationException("Touchscreen HID exposes no input value capabilities.");
                    }

                    nint valueCaps = Marshal.AllocHGlobal(valueCapsCount * NativeTouchMethods.HIDP_VALUE_CAPS_SIZE);
                    try
                    {
                        ushort returned = valueCapsCount;
                        if (NativeTouchMethods.HidP_GetValueCaps(
                                NativeTouchMethods.HIDP_INPUT,
                                valueCaps,
                                ref returned,
                                preparsed) != NativeTouchMethods.HIDP_STATUS_SUCCESS)
                        {
                            throw new InvalidOperationException("HidP_GetValueCaps failed for touchscreen device.");
                        }

                        Dictionary<ushort, ContactLayoutBuilder> builders = [];
                        ushort? countLink = null;
                        for (int index = 0; index < returned; index++)
                        {
                            nint capability = valueCaps + index * NativeTouchMethods.HIDP_VALUE_CAPS_SIZE;
                            ushort usagePage = ReadUInt16(capability, 0);
                            ushort link = ReadUInt16(capability, 6);
                            bool isRange = Marshal.ReadByte(capability, 12) != 0;
                            ushort usageMin = ReadUInt16(capability, 56);
                            ushort usageMax = isRange ? ReadUInt16(capability, 58) : usageMin;
                            int logicalMin = Marshal.ReadInt32(capability, 40);
                            int logicalMax = Marshal.ReadInt32(capability, 44);

                            if (usagePage == NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER &&
                                UsageRangeContains(usageMin, usageMax, NativeTouchMethods.HID_USAGE_DIGITIZER_CONTACT_COUNT))
                            {
                                countLink ??= link;
                            }

                            if (link == 0)
                            {
                                continue;
                            }
                            if (!builders.TryGetValue(link, out ContactLayoutBuilder? builder))
                            {
                                builder = new ContactLayoutBuilder(link);
                                builders[link] = builder;
                            }

                            if (usagePage == NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER &&
                                UsageRangeContains(usageMin, usageMax, NativeTouchMethods.HID_USAGE_DIGITIZER_CONTACT_ID))
                            {
                                builder.HasContactId = true;
                            }
                            else if (usagePage == NativeTouchMethods.HID_USAGE_PAGE_GENERIC &&
                                     UsageRangeContains(usageMin, usageMax, NativeTouchMethods.HID_USAGE_GENERIC_X))
                            {
                                builder.X = new AxisRange(logicalMin, logicalMax);
                            }
                            else if (usagePage == NativeTouchMethods.HID_USAGE_PAGE_GENERIC &&
                                     UsageRangeContains(usageMin, usageMax, NativeTouchMethods.HID_USAGE_GENERIC_Y))
                            {
                                builder.Y = new AxisRange(logicalMin, logicalMax);
                            }
                        }

                        ContactLayout[] contacts = builders.Values
                            .Where(builder => builder.HasContactId && builder.X.HasValue && builder.Y.HasValue)
                            .OrderBy(builder => builder.LinkCollection)
                            .Select(builder => new ContactLayout(
                                builder.LinkCollection,
                                builder.X!.Value,
                                builder.Y!.Value))
                            .ToArray();
                        if (contacts.Length == 0)
                        {
                            throw new InvalidOperationException("Touchscreen HID exposes no contact-id/X/Y collections.");
                        }
                        return new DeviceDescriptor(preparsed, contacts, countLink);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(valueCaps);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(caps);
                }
            }
            catch
            {
                Marshal.FreeHGlobal(preparsed);
                throw;
            }
        }

        public void AppendReport(
            nint report,
            uint reportLength,
            IDictionary<uint, RawTouchContact> contacts,
            ref uint? expectedContactCount)
        {
            if (_contactCountLink is ushort countLink &&
                TryGetValue(
                    NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER,
                    countLink,
                    NativeTouchMethods.HID_USAGE_DIGITIZER_CONTACT_COUNT,
                    report,
                    reportLength,
                    out uint count))
            {
                if (expectedContactCount is null || count > 0)
                {
                    expectedContactCount = count;
                }
                if (count == 0)
                {
                    return;
                }
            }

            foreach (ContactLayout contact in _contacts)
            {
                if (!TryGetValue(
                        NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER,
                        contact.LinkCollection,
                        NativeTouchMethods.HID_USAGE_DIGITIZER_CONTACT_ID,
                        report,
                        reportLength,
                        out uint id) ||
                    !TryGetValue(
                        NativeTouchMethods.HID_USAGE_PAGE_GENERIC,
                        contact.LinkCollection,
                        NativeTouchMethods.HID_USAGE_GENERIC_X,
                        report,
                        reportLength,
                        out uint x) ||
                    !TryGetValue(
                        NativeTouchMethods.HID_USAGE_PAGE_GENERIC,
                        contact.LinkCollection,
                        NativeTouchMethods.HID_USAGE_GENERIC_Y,
                        report,
                        reportLength,
                        out uint y))
                {
                    continue;
                }

                if (TryGetTipState(contact.LinkCollection, report, reportLength, out bool tipDown) && !tipDown)
                {
                    continue;
                }

                TouchPoint point = MapToDesktop(x, y, contact.X, contact.Y);
                contacts[id] = new RawTouchContact(id, point);
            }
        }

        public void Dispose()
        {
            if (_preparsedData != 0)
            {
                Marshal.FreeHGlobal(_preparsedData);
            }
        }

        private bool TryGetValue(
            ushort usagePage,
            ushort linkCollection,
            ushort usage,
            nint report,
            uint reportLength,
            out uint value)
        {
            return NativeTouchMethods.HidP_GetUsageValue(
                       NativeTouchMethods.HIDP_INPUT,
                       usagePage,
                       linkCollection,
                       usage,
                       out value,
                       _preparsedData,
                       report,
                       reportLength) == NativeTouchMethods.HIDP_STATUS_SUCCESS;
        }

        private bool TryGetTipState(
            ushort linkCollection,
            nint report,
            uint reportLength,
            out bool tipDown)
        {
            ushort[] usages = new ushort[16];
            uint usageLength = (uint)usages.Length;
            int status = NativeTouchMethods.HidP_GetUsages(
                NativeTouchMethods.HIDP_INPUT,
                NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER,
                linkCollection,
                usages,
                ref usageLength,
                _preparsedData,
                report,
                reportLength);
            if (status != NativeTouchMethods.HIDP_STATUS_SUCCESS)
            {
                tipDown = true;
                return false;
            }

            tipDown = usages.AsSpan(0, checked((int)Math.Min(usageLength, (uint)usages.Length)))
                .Contains(NativeTouchMethods.HID_USAGE_DIGITIZER_TIP_SWITCH);
            return true;
        }

        private static TouchPoint MapToDesktop(uint rawX, uint rawY, AxisRange xRange, AxisRange yRange)
        {
            int left = NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_XVIRTUALSCREEN);
            int top = NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_YVIRTUALSCREEN);
            int width = NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_CXVIRTUALSCREEN);
            int height = NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_CYVIRTUALSCREEN);
            if (width <= 0 || height <= 0)
            {
                left = 0;
                top = 0;
                width = Math.Max(1, NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_CXSCREEN));
                height = Math.Max(1, NativeTouchMethods.GetSystemMetrics(NativeTouchMethods.SM_CYSCREEN));
            }

            double x = left + Normalize(rawX, xRange) * Math.Max(0, width - 1);
            double y = top + Normalize(rawY, yRange) * Math.Max(0, height - 1);
            return new TouchPoint(x, y);
        }

        private static double Normalize(uint raw, AxisRange range)
        {
            if (range.Maximum <= range.Minimum)
            {
                return 0;
            }
            double value = range.Minimum < 0 ? unchecked((int)raw) : raw;
            return Math.Clamp((value - range.Minimum) / (range.Maximum - (double)range.Minimum), 0, 1);
        }

        private static bool UsageRangeContains(ushort minimum, ushort maximum, ushort usage) =>
            usage >= minimum && usage <= maximum;

        private static ushort ReadUInt16(nint pointer, int offset) => unchecked((ushort)Marshal.ReadInt16(pointer, offset));

        private readonly record struct AxisRange(int Minimum, int Maximum);

        private readonly record struct ContactLayout(ushort LinkCollection, AxisRange X, AxisRange Y);

        private sealed class ContactLayoutBuilder
        {
            public ContactLayoutBuilder(ushort linkCollection) => LinkCollection = linkCollection;

            public ushort LinkCollection { get; }
            public bool HasContactId { get; set; }
            public AxisRange? X { get; set; }
            public AxisRange? Y { get; set; }
        }
    }
}
