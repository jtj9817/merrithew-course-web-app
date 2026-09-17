using System.Diagnostics.Metrics;

namespace CourseInquiryDashboard.Hosting;

/// <summary>
/// Intake and CRM outcome counters (OBS-101 GAP-5) on the standard .NET meter
/// <c>CourseInquiryDashboard</c>, for external monitoring to consume. Instruments
/// carry the outcome as a tag (<c>created</c>/<c>validationRejected</c>/<c>serverError</c>;
/// <c>succeeded</c>/<c>failed</c>/<c>timedOut</c>/<c>cancelled</c>); increments live next
/// to the corresponding sanitized log call sites, so counters never see visitor data.
/// </summary>
public sealed class InquiryMetrics
{
    public const string MeterName = "CourseInquiryDashboard";

    public InquiryMetrics(IMeterFactory factory)
        : this(factory.Create(MeterName))
    {
    }

    public InquiryMetrics(Meter meter)
    {
        IntakeOutcomes = meter.CreateCounter<long>("intake_requests");
        CrmSyncOutcomes = meter.CreateCounter<long>("crm_sync_outcomes");
        CrmSyncRetries = meter.CreateCounter<long>("crm_sync_retries");
    }

    /// <summary>Completed <c>/api/inquiries</c> intake attempts, tagged by outcome.</summary>
    public Counter<long> IntakeOutcomes { get; }

    /// <summary>Final CRM sync outcomes per stored inquiry, tagged by outcome.</summary>
    public Counter<long> CrmSyncOutcomes { get; }

    /// <summary>Retry attempts consumed by the CRM pipeline (attempt count minus one per sync).</summary>
    public Counter<long> CrmSyncRetries { get; }

    public void AddIntake(string outcome) =>
        IntakeOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void AddCrmOutcome(string outcome) =>
        CrmSyncOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
