using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CourseInquiryDashboard.Tests.Fixtures;

public sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    IReadOnlyList<object?> Scopes,
    Exception? Exception);

public sealed class LogCaptureProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
    public void Dispose() { }

    public void AssertPrivacy(IEnumerable<string> sentinels)
    {
        var forbidden = sentinels.ToArray();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // OBS-101 additions: request-outcome/validation entries log request shape and
            // failing field names only — never attempted values or visitor data.
            "inquiryId", "attempt", "outcome", "duration", "errorCategory", "errorType",
            "method", "route", "statusCode", "fields", "{OriginalFormat}"
        };
        foreach (var entry in Entries)
        {
            Assert.Null(entry.Exception);
            var surfaces = new List<string> { entry.Message };
            surfaces.AddRange(entry.State.Select(pair => $"{pair.Key}={pair.Value}"));
            foreach (var scope in entry.Scopes)
            {
                surfaces.Add(scope?.ToString() ?? string.Empty);
                if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                    surfaces.AddRange(values.Select(pair => $"{pair.Key}={pair.Value}"));
            }
            foreach (var value in forbidden)
                Assert.DoesNotContain(surfaces, text => text.Contains(value, StringComparison.OrdinalIgnoreCase));
            if (entry.Category.StartsWith("CourseInquiryDashboard.", StringComparison.Ordinal))
                Assert.All(entry.State, pair => Assert.Contains(pair.Key, allowed));
        }
    }

    private sealed class CaptureLogger(LogCaptureProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var capturedScopes = new List<object?>();
            owner.scopes.ForEachScope((scope, list) => list.Add(
                scope is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToArray() : scope), capturedScopes);
            var values = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? structured.ToArray() : new[] { new KeyValuePair<string, object?>("state", state) };
            owner.Entries.Enqueue(new CapturedLog(category, level, eventId, formatter(state, exception), values, capturedScopes, exception));
        }
    }
}
