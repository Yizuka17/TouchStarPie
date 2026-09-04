using WinPieGestures.WinUI.Input;

static class Program
{
    private static int _passed;

    public static int Main()
    {
        Run("8-direction quantization", DirectionQuantization);
        Run("secondary sector quantization", SecondarySectorQuantization);
        Run("long press arms before invocation", LongPressOnlyArms);
        Run("single-finger armed swipe invokes east", SingleFingerGesture);
        Run("armed release is a no-op", ArmedReleaseDoesNothing);
        Run("two-finger chord waits for final finger", TwoFingerGesture);
        Run("three-finger chord invokes from centroid", ThreeFingerGesture);
        Run("movement before hold suppresses candidate", EarlyMovementSuppressesCandidate);
        Run("disabled finger count is suppressed", DisabledFingerCountIsSuppressed);
        Run("contact-set change after arm suppresses chord", ContactChangeAfterArmSuppressesChord);
        Console.WriteLine($"All {_passed} touch input tests passed.");
        return 0;
    }

    private static void DirectionQuantization()
    {
        Equal(0, TouchGestureRecognizer.QuantizeDirection(-Math.PI / 2, 8), "north");
        Equal(2, TouchGestureRecognizer.QuantizeDirection(0, 8), "east");
        Equal(4, TouchGestureRecognizer.QuantizeDirection(Math.PI / 2, 8), "south");
        Equal(6, TouchGestureRecognizer.QuantizeDirection(Math.PI, 8), "west");
        Equal(1, TouchGestureRecognizer.QuantizeDirection(-Math.PI / 4, 8), "north-east");
    }

    private static void SecondarySectorQuantization()
    {
        // East is main sector 2 in the 8-way convention. Its 45-degree parent span is
        // divided into four children from north-east side to south-east side.
        Equal(0, RadialSelectionMath.QuantizeSub(-Math.PI / 8 + 0.01, 2, 8, 4), "east child 0");
        Equal(1, RadialSelectionMath.QuantizeSub(-Math.PI / 16, 2, 8, 4), "east child 1");
        Equal(2, RadialSelectionMath.QuantizeSub(Math.PI / 16, 2, 8, 4), "east child 2");
        Equal(3, RadialSelectionMath.QuantizeSub(Math.PI / 8 - 0.01, 2, 8, 4), "east child 3");
    }

    private static void LongPressOnlyArms()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activated = 0;
        recognizer.Activated += (_, _) => activated++;

        recognizer.PointerDown(1, new TouchPoint(100, 100), start);
        recognizer.Tick(start.AddMilliseconds(421));

        Equal(TouchGesturePhase.Armed, recognizer.Phase, "phase after long press");
        Equal(0, activated, "long press must not invoke wheel");

        recognizer.PointerMove(1, new TouchPoint(120, 100), start.AddMilliseconds(430));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "sub-threshold movement stays armed");
        Equal(0, activated, "sub-threshold movement must not invoke wheel");
    }

    private static void SingleFingerGesture()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        TouchGestureActivation? activation = null;
        TouchGestureCompletion? completion = null;
        recognizer.Activated += (_, value) => activation = value;
        recognizer.Completed += (_, value) => completion = value;

        recognizer.PointerDown(1, new TouchPoint(100, 100), start);
        recognizer.Tick(start.AddMilliseconds(421));
        recognizer.PointerMove(1, new TouchPoint(150, 100), start.AddMilliseconds(440));
        recognizer.PointerUp(1, new TouchPoint(150, 100), start.AddMilliseconds(450));

        True(activation.HasValue, "gesture should invoke after armed movement");
        Equal(1, activation!.Value.FingerCount, "finger count");
        True(completion.HasValue && completion.Value.HasDirection, "direction should complete");
        Equal(2, completion!.Value.DirectionIndex, "east direction");
    }

    private static void ArmedReleaseDoesNothing()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activated = 0;
        int completed = 0;
        recognizer.Activated += (_, _) => activated++;
        recognizer.Completed += (_, _) => completed++;

        recognizer.PointerDown(1, new TouchPoint(40, 40), start);
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "armed phase");
        recognizer.PointerUp(1, new TouchPoint(40, 40), start.AddMilliseconds(450));

        Equal(TouchGesturePhase.Idle, recognizer.Phase, "release returns idle");
        Equal(0, activated, "no invocation");
        Equal(0, completed, "no completion");
    }

    private static void TwoFingerGesture()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activations = 0;
        int completions = 0;
        TouchGestureCompletion result = default;
        recognizer.Activated += (_, _) => activations++;
        recognizer.Completed += (_, value) => { completions++; result = value; };

        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerDown(2, new TouchPoint(100, 0), start.AddMilliseconds(20));
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.Holding, recognizer.Phase, "chord must wait from final finger down");
        recognizer.Tick(start.AddMilliseconds(441));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "two-finger chord armed");
        Equal(0, activations, "arming is not invocation");

        recognizer.PointerMove(1, new TouchPoint(0, 60), start.AddMilliseconds(450));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "first contact move keeps centroid below threshold");
        recognizer.PointerMove(2, new TouchPoint(100, 60), start.AddMilliseconds(455));
        Equal(TouchGesturePhase.Active, recognizer.Phase, "centroid movement invokes chord");
        Equal(1, activations, "activation count");

        recognizer.PointerUp(1, new TouchPoint(0, 60), start.AddMilliseconds(460));
        Equal(0, completions, "first lift must not complete a two-finger chord");
        recognizer.PointerUp(2, new TouchPoint(100, 60), start.AddMilliseconds(470));

        Equal(1, completions, "completion count");
        Equal(2, result.FingerCount, "locked finger count");
        Equal(4, result.DirectionIndex, "south direction");
    }

    private static void ThreeFingerGesture()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        TouchGestureActivation? activation = null;
        recognizer.Activated += (_, value) => activation = value;

        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerDown(2, new TouchPoint(30, 0), start.AddMilliseconds(10));
        recognizer.PointerDown(3, new TouchPoint(60, 0), start.AddMilliseconds(20));
        recognizer.Tick(start.AddMilliseconds(441));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "three-finger chord armed");

        recognizer.PointerMove(1, new TouchPoint(0, -50), start.AddMilliseconds(450));
        recognizer.PointerMove(2, new TouchPoint(30, -50), start.AddMilliseconds(455));
        recognizer.PointerMove(3, new TouchPoint(60, -50), start.AddMilliseconds(460));

        True(activation.HasValue, "three-finger chord should invoke");
        Equal(3, activation!.Value.FingerCount, "three-finger activation count");
    }

    private static void EarlyMovementSuppressesCandidate()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activated = 0;
        recognizer.Activated += (_, _) => activated++;
        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerMove(1, new TouchPoint(25, 0), start.AddMilliseconds(60));
        Equal(TouchGesturePhase.Suppressed, recognizer.Phase, "phase");
        Equal(0, activated, "activation count");
        recognizer.PointerUp(1, new TouchPoint(25, 0), start.AddMilliseconds(80));
        Equal(TouchGesturePhase.Idle, recognizer.Phase, "suppression clears after release");
    }

    private static void DisabledFingerCountIsSuppressed()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        recognizer.EnableThreeFinger = false;
        DateTimeOffset start = DateTimeOffset.UtcNow;
        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerDown(2, new TouchPoint(20, 0), start);
        recognizer.PointerDown(3, new TouchPoint(40, 0), start);
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.Suppressed, recognizer.Phase, "disabled chord phase");
    }

    private static void ContactChangeAfterArmSuppressesChord()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activated = 0;
        recognizer.Activated += (_, _) => activated++;

        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.Armed, recognizer.Phase, "single finger armed");
        recognizer.PointerDown(2, new TouchPoint(20, 0), start.AddMilliseconds(430));

        Equal(TouchGesturePhase.Suppressed, recognizer.Phase, "contact change suppresses armed chord");
        Equal(0, activated, "must not invoke stale chord");
    }

    private static TouchGestureRecognizer CreateRecognizer() => new()
    {
        LongPressDelayMs = 420,
        HoldMovementTolerance = 18,
        SwipeThreshold = 34,
        DirectionCount = 8
    };

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL  {name}: {exception.Message}");
            Environment.ExitCode = 1;
            throw;
        }
    }

    private static void Equal<T>(T expected, T actual, string label) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void True(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }
}
