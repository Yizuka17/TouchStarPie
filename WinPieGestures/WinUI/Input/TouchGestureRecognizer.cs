namespace WinPieGestures.WinUI.Input;

public readonly record struct TouchPoint(double X, double Y)
{
    public static TouchPoint Center(IEnumerable<TouchPoint> points)
    {
        double x = 0;
        double y = 0;
        int count = 0;
        foreach (TouchPoint point in points)
        {
            x += point.X;
            y += point.Y;
            count++;
        }
        return count == 0 ? default : new TouchPoint(x / count, y / count);
    }

    public double DistanceTo(TouchPoint other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public readonly record struct TouchContact(uint Id, TouchPoint Start, TouchPoint Current);

public readonly record struct TouchGestureActivation(int FingerCount, TouchPoint Center);

public readonly record struct TouchGestureUpdate(
    int FingerCount,
    TouchPoint Center,
    double Angle,
    double Distance,
    int DirectionIndex,
    bool HasDirection);

public readonly record struct TouchGestureCompletion(
    int FingerCount,
    TouchPoint Center,
    double Angle,
    double Distance,
    int DirectionIndex,
    bool HasDirection);

public enum TouchGesturePhase
{
    Idle,
    Holding,
    Armed,
    PassThrough
}

/// <summary>
/// Framework-independent multi-contact state machine. Direction zero is north and indices
/// increase clockwise, matching StarPie's radial sector convention in screen coordinates.
/// </summary>
public sealed class TouchGestureRecognizer
{
    private sealed class ContactState
    {
        public ContactState(uint id, TouchPoint point)
        {
            Id = id;
            Start = point;
            Current = point;
        }

        public uint Id { get; }
        public TouchPoint Start { get; }
        public TouchPoint Current { get; set; }
    }

    private readonly Dictionary<uint, ContactState> _contacts = [];
    private DateTimeOffset _startedAt;
    private TouchPoint _activationCenter;
    private int _lockedFingerCount;
    private TouchGestureUpdate _lastUpdate;
    private bool _releasing;

    public TouchGesturePhase Phase { get; private set; }

    public double LongPressDelayMs { get; set; } = 420;
    public double HoldMovementTolerance { get; set; } = 18;
    public double SwipeThreshold { get; set; } = 34;
    public int DirectionCount { get; set; } = 8;
    public bool EnableOneFinger { get; set; } = true;
    public bool EnableTwoFinger { get; set; } = true;
    public bool EnableThreeFinger { get; set; } = true;

    public IReadOnlyList<TouchContact> Contacts => _contacts.Values
        .Select(contact => new TouchContact(contact.Id, contact.Start, contact.Current))
        .ToArray();

    public event EventHandler<TouchGestureActivation>? Activated;
    public event EventHandler<TouchGestureUpdate>? Updated;
    public event EventHandler<TouchGestureCompletion>? Completed;
    public event EventHandler? PassThroughStarted;
    public event EventHandler? SessionEnded;

    public void PointerDown(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (Phase == TouchGesturePhase.Idle)
        {
            _startedAt = timestamp;
            Phase = TouchGesturePhase.Holding;
        }

        if (_contacts.ContainsKey(pointerId))
        {
            return;
        }

        _contacts[pointerId] = new ContactState(pointerId, point);
        if (Phase == TouchGesturePhase.Holding)
        {
            // A chord starts only after its final finger arrives. Without resetting the
            // clock, a second or third finger added near the deadline could arm instantly.
            _startedAt = timestamp;
        }
        if (_contacts.Count > 3 || Phase == TouchGesturePhase.Armed)
        {
            BeginPassThrough();
        }
    }

    public void PointerMove(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (!_contacts.TryGetValue(pointerId, out ContactState? contact))
        {
            return;
        }

        contact.Current = point;
        if (Phase == TouchGesturePhase.Holding &&
            _contacts.Values.Any(value => value.Start.DistanceTo(value.Current) > HoldMovementTolerance))
        {
            BeginPassThrough();
        }
        else if (Phase == TouchGesturePhase.Armed && !_releasing)
        {
            RaiseUpdate();
        }

        Tick(timestamp);
    }

    public void PointerUp(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (!_contacts.TryGetValue(pointerId, out ContactState? contact))
        {
            return;
        }

        contact.Current = point;
        Tick(timestamp);
        TouchGesturePhase phaseBeforeRemoval = Phase;

        if (phaseBeforeRemoval == TouchGesturePhase.Armed && !_releasing)
        {
            RaiseUpdate();
            _releasing = true;
        }
        else if (phaseBeforeRemoval == TouchGesturePhase.Holding)
        {
            // Mark short taps as pass-through while the original contacts are still available
            // to the injection layer.
            BeginPassThrough();
            phaseBeforeRemoval = TouchGesturePhase.PassThrough;
        }

        _contacts.Remove(pointerId);
        if (_contacts.Count != 0)
        {
            return;
        }

        if (phaseBeforeRemoval == TouchGesturePhase.Armed)
        {
            Completed?.Invoke(this, new TouchGestureCompletion(
                _lockedFingerCount,
                _lastUpdate.Center,
                _lastUpdate.Angle,
                _lastUpdate.Distance,
                _lastUpdate.DirectionIndex,
                _lastUpdate.HasDirection));
        }
        Reset();
    }

    public void Tick(DateTimeOffset timestamp)
    {
        if (Phase != TouchGesturePhase.Holding || _contacts.Count == 0)
        {
            return;
        }

        if ((timestamp - _startedAt).TotalMilliseconds < LongPressDelayMs)
        {
            return;
        }

        int fingerCount = _contacts.Count;
        bool enabled = fingerCount switch
        {
            1 => EnableOneFinger,
            2 => EnableTwoFinger,
            3 => EnableThreeFinger,
            _ => false
        };

        if (!enabled)
        {
            BeginPassThrough();
            return;
        }

        _lockedFingerCount = fingerCount;
        _activationCenter = CurrentCenter();
        _lastUpdate = new TouchGestureUpdate(fingerCount, _activationCenter, 0, 0, -1, false);
        Phase = TouchGesturePhase.Armed;
        Activated?.Invoke(this, new TouchGestureActivation(fingerCount, _activationCenter));
    }

    public void Cancel()
    {
        if (_contacts.Count > 0 && Phase is TouchGesturePhase.Holding or TouchGesturePhase.Armed)
        {
            BeginPassThrough();
        }
        if (_contacts.Count == 0)
        {
            Reset();
        }
    }

    private void RaiseUpdate()
    {
        TouchPoint center = CurrentCenter();
        double dx = center.X - _activationCenter.X;
        double dy = center.Y - _activationCenter.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx);
        bool hasDirection = distance >= SwipeThreshold;
        int index = hasDirection ? QuantizeDirection(angle, DirectionCount) : -1;
        _lastUpdate = new TouchGestureUpdate(
            _lockedFingerCount, center, angle, distance, index, hasDirection);
        Updated?.Invoke(this, _lastUpdate);
    }

    private void BeginPassThrough()
    {
        if (Phase == TouchGesturePhase.PassThrough)
        {
            return;
        }
        Phase = TouchGesturePhase.PassThrough;
        PassThroughStarted?.Invoke(this, EventArgs.Empty);
    }

    private void Reset()
    {
        _contacts.Clear();
        Phase = TouchGesturePhase.Idle;
        _lockedFingerCount = 0;
        _releasing = false;
        _lastUpdate = default;
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    private TouchPoint CurrentCenter() => TouchPoint.Center(_contacts.Values.Select(contact => contact.Current));

    public static int QuantizeDirection(double angle, int directionCount)
    {
        int count = directionCount == 4 ? 4 : 8;
        double step = Math.Tau / count;
        int index = (int)Math.Round((angle + Math.PI / 2) / step, MidpointRounding.AwayFromZero);
        return ((index % count) + count) % count;
    }
}
