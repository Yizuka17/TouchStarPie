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
    Active,
    Suppressed
}

/// <summary>
/// Framework-independent multi-contact state machine. A stable long press only arms the
/// gesture; the wheel is invoked after the armed centroid moves past SwipeThreshold.
/// Direction zero is north and indices increase clockwise in screen coordinates.
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
        public TouchPoint Start { get; set; }
        public TouchPoint Current { get; set; }
    }

    private readonly Dictionary<uint, ContactState> _contacts = [];
    private DateTimeOffset _startedAt;
    private TouchPoint _armedCenter;
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
    public event EventHandler? Canceled;
    public event EventHandler? SessionEnded;

    public void PointerDown(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (_contacts.ContainsKey(pointerId))
        {
            return;
        }

        _contacts[pointerId] = new ContactState(pointerId, point);
        if (Phase == TouchGesturePhase.Idle)
        {
            Phase = TouchGesturePhase.Holding;
            ResetHoldingBaseline(timestamp);
            return;
        }

        if (Phase == TouchGesturePhase.Holding)
        {
            if (_contacts.Count > 3)
            {
                EnterSuppressed();
            }
            else
            {
                // A multi-finger chord starts when the last finger arrives. Reset both the
                // timer and movement baselines so an earlier finger cannot arm the chord.
                ResetHoldingBaseline(timestamp);
            }
            return;
        }

        // Once armed, changing the contact set invalidates the chord. During an active
        // wheel gesture this also closes the wheel rather than executing a stale direction.
        if (Phase is TouchGesturePhase.Armed or TouchGesturePhase.Active)
        {
            EnterSuppressed();
        }
    }

    public void PointerMove(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (!_contacts.TryGetValue(pointerId, out ContactState? contact))
        {
            return;
        }

        contact.Current = point;
        if (Phase == TouchGesturePhase.Holding)
        {
            if (_contacts.Values.Any(value => value.Start.DistanceTo(value.Current) > HoldMovementTolerance))
            {
                EnterSuppressed();
                return;
            }
            Tick(timestamp);
            return;
        }

        if (Phase == TouchGesturePhase.Armed)
        {
            if (_contacts.Count != _lockedFingerCount)
            {
                EnterSuppressed();
                return;
            }
            TryActivate();
            return;
        }

        if (Phase == TouchGesturePhase.Active && !_releasing)
        {
            RaiseUpdate();
        }
    }

    public void PointerUp(uint pointerId, TouchPoint point, DateTimeOffset timestamp)
    {
        if (!_contacts.TryGetValue(pointerId, out ContactState? contact))
        {
            return;
        }

        contact.Current = point;

        if (Phase == TouchGesturePhase.Holding)
        {
            _contacts.Remove(pointerId);
            if (_contacts.Count == 0)
            {
                Reset();
            }
            else
            {
                // Before arming, a changed chord becomes a fresh candidate.
                ResetHoldingBaseline(timestamp);
            }
            return;
        }

        if (Phase == TouchGesturePhase.Armed)
        {
            // Holding still and lifting is intentionally a no-op.
            _contacts.Remove(pointerId);
            if (_contacts.Count == 0)
            {
                Reset();
            }
            else
            {
                EnterSuppressed();
            }
            return;
        }

        if (Phase == TouchGesturePhase.Active)
        {
            if (!_releasing)
            {
                RaiseUpdate();
                _releasing = true;
            }
            _contacts.Remove(pointerId);
            if (_contacts.Count == 0)
            {
                Completed?.Invoke(this, new TouchGestureCompletion(
                    _lockedFingerCount,
                    _lastUpdate.Center,
                    _lastUpdate.Angle,
                    _lastUpdate.Distance,
                    _lastUpdate.DirectionIndex,
                    _lastUpdate.HasDirection));
                Reset();
            }
            return;
        }

        _contacts.Remove(pointerId);
        if (_contacts.Count == 0)
        {
            Reset();
        }
    }

    public void Tick(DateTimeOffset timestamp)
    {
        if (Phase != TouchGesturePhase.Holding || _contacts.Count == 0)
        {
            return;
        }
        if (_contacts.Values.Any(value => value.Start.DistanceTo(value.Current) > HoldMovementTolerance))
        {
            EnterSuppressed();
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
            EnterSuppressed();
            return;
        }

        _lockedFingerCount = fingerCount;
        _armedCenter = CurrentCenter();
        _lastUpdate = new TouchGestureUpdate(fingerCount, _armedCenter, 0, 0, -1, false);
        Phase = TouchGesturePhase.Armed;
    }

    /// <summary>Suppress the current physical contact sequence until every finger is lifted.</summary>
    public void Suppress()
    {
        if (_contacts.Count == 0)
        {
            Reset();
            return;
        }
        EnterSuppressed();
    }

    /// <summary>Immediately abandons all tracked contacts, used when the raw input service stops.</summary>
    public void Cancel()
    {
        if (Phase == TouchGesturePhase.Active)
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
        Reset();
    }

    private void ResetHoldingBaseline(DateTimeOffset timestamp)
    {
        _startedAt = timestamp;
        foreach (ContactState contact in _contacts.Values)
        {
            contact.Start = contact.Current;
        }
    }

    private void TryActivate()
    {
        TouchPoint center = CurrentCenter();
        if (center.DistanceTo(_armedCenter) < SwipeThreshold)
        {
            return;
        }

        Phase = TouchGesturePhase.Active;
        Activated?.Invoke(this, new TouchGestureActivation(_lockedFingerCount, _armedCenter));
        RaiseUpdate();
    }

    private void RaiseUpdate()
    {
        TouchPoint center = CurrentCenter();
        double dx = center.X - _armedCenter.X;
        double dy = center.Y - _armedCenter.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx);
        bool hasDirection = distance >= SwipeThreshold;
        int index = hasDirection ? QuantizeDirection(angle, DirectionCount) : -1;
        _lastUpdate = new TouchGestureUpdate(
            _lockedFingerCount, center, angle, distance, index, hasDirection);
        Updated?.Invoke(this, _lastUpdate);
    }

    private void EnterSuppressed()
    {
        if (Phase == TouchGesturePhase.Suppressed)
        {
            return;
        }
        if (Phase == TouchGesturePhase.Active)
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
        Phase = TouchGesturePhase.Suppressed;
        _releasing = false;
    }

    private void Reset()
    {
        bool hadSession = Phase != TouchGesturePhase.Idle || _contacts.Count != 0;
        _contacts.Clear();
        Phase = TouchGesturePhase.Idle;
        _lockedFingerCount = 0;
        _releasing = false;
        _lastUpdate = default;
        if (hadSession)
        {
            SessionEnded?.Invoke(this, EventArgs.Empty);
        }
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
