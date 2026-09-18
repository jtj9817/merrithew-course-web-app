using System.ComponentModel.DataAnnotations;
using CourseInquiryDashboard.DevTools;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-FAULT cases: the dev-only intake fault switch — inert by default, bounded
/// arm/consume/disarm behavior, range validation, and thread-safe consumption so no
/// two concurrent creates can share one armed slot.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IntakeFaultRuntimeTests
{
    [Fact]
    [Trait("CaseId", "UT-FAULT-001")]
    public void Default_runtime_is_inert()
    {
        var runtime = new IntakeFaultRuntime();

        Assert.Equal(0, runtime.Armed);
        Assert.Equal(0, runtime.TotalInjected);
        Assert.False(runtime.TryConsume());
        Assert.Equal(0, runtime.TotalInjected);
    }

    [Fact]
    [Trait("CaseId", "UT-FAULT-002")]
    public void Arm_consumes_exactly_the_armed_count_then_goes_inert()
    {
        var runtime = new IntakeFaultRuntime();

        runtime.Arm(3);
        Assert.Equal(3, runtime.Armed);

        Assert.True(runtime.TryConsume());
        Assert.True(runtime.TryConsume());
        Assert.Equal(1, runtime.Armed);
        Assert.True(runtime.TryConsume());

        Assert.False(runtime.TryConsume()); // exhausted
        Assert.Equal(0, runtime.Armed);
        Assert.Equal(3, runtime.TotalInjected);
    }

    [Fact]
    [Trait("CaseId", "UT-FAULT-003")]
    public void Disarm_clears_the_armed_counter_but_keeps_the_injected_total()
    {
        var runtime = new IntakeFaultRuntime();
        runtime.Arm(5);
        Assert.True(runtime.TryConsume());

        runtime.Disarm();

        Assert.Equal(0, runtime.Armed);
        Assert.Equal(1, runtime.TotalInjected);
        Assert.False(runtime.TryConsume());
    }

    [Fact]
    [Trait("CaseId", "UT-FAULT-004")]
    public void Arm_zero_disarms()
    {
        var runtime = new IntakeFaultRuntime();
        runtime.Arm(2);

        runtime.Arm(0);

        Assert.Equal(0, runtime.Armed);
        Assert.False(runtime.TryConsume());
    }

    [Theory]
    [Trait("CaseId", "UT-FAULT-005")]
    [InlineData(-1)]
    [InlineData(IntakeFaultRuntime.MaxArmCount + 1)]
    public void Arm_rejects_out_of_range_counts(int count)
    {
        var runtime = new IntakeFaultRuntime();

        Assert.Throws<ValidationException>(() => runtime.Arm(count));
        Assert.Equal(0, runtime.Armed);
    }

    [Fact]
    [Trait("CaseId", "UT-FAULT-006")]
    public async Task Concurrent_consumption_never_over_or_under_consumes()
    {
        var runtime = new IntakeFaultRuntime();
        const int armed = 50;
        const int racers = 200;
        runtime.Arm(armed);

        var consumed = 0;
        var tasks = Enumerable.Range(0, racers).Select(_ => Task.Run(() =>
        {
            if (runtime.TryConsume())
                Interlocked.Increment(ref consumed);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(armed, consumed);
        Assert.Equal(0, runtime.Armed);
        Assert.Equal(armed, runtime.TotalInjected);
    }
}
