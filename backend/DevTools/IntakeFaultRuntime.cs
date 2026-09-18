using System.ComponentModel.DataAnnotations;

namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only fault switch for the intake write path. Holds a bounded "arm the next
/// N creates to fail" counter (default <c>0</c> = inert) and a running total of
/// injected faults for the demonstration readout. Access is thread-safe via
/// interlocked operations, and the shape mirrors
/// <see cref="Services.CrmSimulationRuntime"/>. The only way to arm it is the
/// dev-gated endpoint, so an unmapped (production) runtime is permanently inert.
/// </summary>
public sealed class IntakeFaultRuntime
{
    /// <summary>Upper bound on how many consecutive creates may be armed to fail.</summary>
    public const int MaxArmCount = 100;

    private int armed;
    private long totalInjected;

    /// <summary>Creates still queued to fail on their next attempt (<c>0</c> = inert).</summary>
    public int Armed => Volatile.Read(ref armed);

    /// <summary>How many creates this runtime has failed since the process started.</summary>
    public long TotalInjected => Interlocked.Read(ref totalInjected);

    /// <summary>Arms the next <paramref name="count"/> creates to fail; <c>0</c> disarms.</summary>
    /// <exception cref="ValidationException">The count is outside <c>0..MaxArmCount</c>.</exception>
    public void Arm(int count)
    {
        if (count < 0 || count > MaxArmCount)
            throw new ValidationException($"The armed count must be between 0 and {MaxArmCount}.");

        Volatile.Write(ref armed, count);
    }

    /// <summary>Clears the armed counter without changing the injected total.</summary>
    public void Disarm() => Volatile.Write(ref armed, 0);

    /// <summary>
    /// Atomically consumes one armed fault. Returns <see langword="true"/> — and
    /// decrements the armed counter while incrementing the injected total — when a
    /// fault was armed; <see langword="false"/> when inert. Safe under concurrent
    /// creates, so no two requests can consume the same armed slot.
    /// </summary>
    public bool TryConsume()
    {
        int current;
        do
        {
            current = Volatile.Read(ref armed);
            if (current <= 0)
                return false;
        }
        while (Interlocked.CompareExchange(ref armed, current - 1, current) != current);

        Interlocked.Increment(ref totalInjected);
        return true;
    }
}

/// <summary>
/// Thrown by the intake write path when a dev-armed fault fires, before any row is
/// persisted. Its type name is what the error middleware surfaces as the sanitized
/// <c>ErrorType</c> on the resulting 500 — deliberately non-PII.
/// </summary>
public sealed class IntakeFaultInjectedException()
    : Exception("Injected intake fault (development only).");
