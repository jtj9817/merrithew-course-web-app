using Microsoft.Extensions.Logging;

namespace CourseInquiryDashboard.Hosting;

/// <summary>
/// Request-outcome logging (OBS-101 GAP-4): one terminal entry per completed
/// <c>/api/inquiries</c> request with method, route template, status, and outcome
/// category. Level follows the status class; exceptions are never attached — the
/// error middleware stays the single place raw exception types are logged (C6).
/// The request's correlation ID comes from the outermost correlation scope.
/// </summary>
internal static partial class RequestOutcomeLogger
{
    public const string Category = "CourseInquiryDashboard.RequestOutcome";

    [LoggerMessage(EventId = 20, Level = LogLevel.Information,
        Message = "Request {Method} {Route} completed with status {StatusCode} and outcome {Outcome}")]
    public static partial void Succeeded(ILogger logger, string method, string route, int statusCode, string outcome);

    [LoggerMessage(EventId = 21, Level = LogLevel.Warning,
        Message = "Request {Method} {Route} completed with status {StatusCode} and outcome {Outcome}")]
    public static partial void ClientError(ILogger logger, string method, string route, int statusCode, string outcome);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error,
        Message = "Request {Method} {Route} completed with status {StatusCode} and outcome {Outcome}")]
    public static partial void ServerError(ILogger logger, string method, string route, int statusCode, string outcome);

    public static string OutcomeFor(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "succeeded",
        >= 400 and < 500 => "clientError",
        >= 500 => "serverError",
        _ => "other",
    };
}

/// <summary>
/// Validation-rejection logging (OBS-101 GAP-1): exactly one warning per request
/// the automatic 400 pipeline rejects. Logs failing field keys only — attempted
/// values contain visitor data and must never reach a sink (C6).
/// </summary>
internal static partial class ValidationRejectionLogger
{
    public const string Category = "CourseInquiryDashboard.Validation";

    [LoggerMessage(EventId = 23, Level = LogLevel.Warning,
        Message = "Request {Method} {Route} rejected with outcome {Outcome}; invalid fields: {Fields}")]
    public static partial void Rejected(ILogger logger, string method, string route, string outcome, string fields);
}
