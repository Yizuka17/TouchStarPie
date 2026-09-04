using WinPieGestures.WinUI.Input;

static class Program
{
    private static int _passed;

    public static int Main()
    {
        Run("8-direction quantization", DirectionQuantization);
        Run("single-finger long press and east swipe", SingleFingerGesture);
        Run("two-finger centroid remains stable during release", TwoFingerGesture);
        Run("movement before hold enters pass-through", EarlyMovementPassesThrough);
        Run("disabled finger count passes through", DisabledFingerCountPassesThrough);
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

        True(activation.HasValue, "gesture should arm");
        Equal(1, activation!.Value.FingerCount, "finger count");
        True(completion.HasValue && completion.Value.HasDirection, "direction should complete");
        Equal(2, completion!.Value.DirectionIndex, "east direction");
    }

    private static void TwoFingerGesture()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int completions = 0;
        TouchGestureCompletion result = default;
        recognizer.Completed += (_, value) => { completions++; result = value; };

        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerDown(2, new TouchPoint(100, 0), start.AddMilliseconds(20));
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.Holding, recognizer.Phase, "chord must wait from final finger down");
        recognizer.Tick(start.AddMilliseconds(441));
        recognizer.PointerMove(1, new TouchPoint(0, 60), start.AddMilliseconds(450));
        recognizer.PointerMove(2, new TouchPoint(100, 60), start.AddMilliseconds(455));
        recognizer.PointerUp(1, new TouchPoint(0, 60), start.AddMilliseconds(460));
        Equal(0, completions, "first lift must not complete a two-finger chord");
        recognizer.PointerUp(2, new TouchPoint(100, 60), start.AddMilliseconds(470));

        Equal(1, completions, "completion count");
        Equal(2, result.FingerCount, "locked finger count");
        Equal(4, result.DirectionIndex, "south direction");
    }

    private static void EarlyMovementPassesThrough()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int activated = 0;
        int passThrough = 0;
        recognizer.Activated += (_, _) => activated++;
        recognizer.PassThroughStarted += (_, _) => passThrough++;
        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerMove(1, new TouchPoint(25, 0), start.AddMilliseconds(60));
        Equal(TouchGesturePhase.PassThrough, recognizer.Phase, "phase");
        Equal(0, activated, "activation count");
        Equal(1, passThrough, "pass-through count");
    }

    private static void DisabledFingerCountPassesThrough()
    {
        TouchGestureRecognizer recognizer = CreateRecognizer();
        recognizer.EnableThreeFinger = false;
        DateTimeOffset start = DateTimeOffset.UtcNow;
        recognizer.PointerDown(1, new TouchPoint(0, 0), start);
        recognizer.PointerDown(2, new TouchPoint(20, 0), start);
        recognizer.PointerDown(3, new TouchPoint(40, 0), start);
        recognizer.Tick(start.AddMilliseconds(421));
        Equal(TouchGesturePhase.PassThrough, recognizer.Phase, "disabled chord phase");
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
