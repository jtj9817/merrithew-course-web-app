using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using CourseInquiryDashboard.DevTools;
using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;
using CourseInquiryDashboard.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new StatusJsonConverter()));
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(); // liveness + DB readiness in one endpoint (OBS-101 GAP-5)
builder.Services.AddSingleton(sp => new InquiryMetrics(sp.GetRequiredService<IMeterFactory>()));

// OBS-101 GAP-1: the automatic 400 pipeline runs before any action code, so this is
// the single seam that can log a rejected submission and stamp its correlation ID.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var http = context.HttpContext;
        var loggerFactory = http.RequestServices.GetRequiredService<ILoggerFactory>();

        ValidationRejectionLogger.Rejected(
            loggerFactory.CreateLogger(ValidationRejectionLogger.Category),
            http.Request.Method,
            context.ActionDescriptor.AttributeRouteInfo?.Template ?? http.Request.Path.ToString(),
            "validationRejected",
            string.Join("; ", context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => entry.Key))); // property names only — never attempted values (C6)

        http.RequestServices.GetRequiredService<InquiryMetrics>().AddIntake("validationRejected");

        var problem = http.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(http, context.ModelState, StatusCodes.Status400BadRequest);
        problem.Extensions["traceId"] = http.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.MapType<Status>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = StatusNames.All.Select(name => (JsonNode)JsonValue.Create(name)!).ToList(),
    }));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<CrmSimulationOptions>()
    .BindConfiguration(CrmSimulationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<CrmSimulationRuntime>();
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddSingleton<ICrmClient, SimulatedCrmClient>();
builder.Services.AddScoped<IScenarioSeeder, ScenarioSeeder>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("The database connection is not configured.")));

// C6 privacy policy: EF Core attaches raw provider exceptions (with SQL and parameters)
// to Error-level entries; keep the whole EF surface out of sinks below Critical.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);

var app = builder.Build();

// OBS-101 GAP-2: outermost scope carrying the request's correlation ID, so
// request-outcome, validation-rejection, creation, CRM, and 5xx entries all
// share one identifier a staff report can be tied to.
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CourseInquiryDashboard.Correlation");
    using var scope = logger.BeginScope(
        new Dictionary<string, object> { ["correlationId"] = context.TraceIdentifier });
    await next(context);
});

// OBS-101 GAP-4: one terminal outcome entry per completed /api/inquiries request.
// Wraps the error middleware so converted 500s are recorded with a status, never
// with an exception (C6 — exception detail stays in the error middleware).
app.Use(async (context, next) =>
{
    var isIntakeRequest = context.Request.Path.StartsWithSegments("/api/inquiries");
    try
    {
        await next(context);
    }
    finally
    {
        if (isIntakeRequest)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(RequestOutcomeLogger.Category);
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                ?? context.Request.Path.ToString();
            var statusCode = context.Response.StatusCode;
            var outcome = RequestOutcomeLogger.OutcomeFor(statusCode);
            if (statusCode >= 500)
                RequestOutcomeLogger.ServerError(logger, context.Request.Method, route, statusCode, outcome);
            else if (statusCode >= 400)
                RequestOutcomeLogger.ClientError(logger, context.Request.Method, route, statusCode, outcome);
            else
                RequestOutcomeLogger.Succeeded(logger, context.Request.Method, route, statusCode, outcome);
        }
    }
});

// C3: sanitized 500 ProblemDetails in both Development and Production — the developer
// page must never leak stack traces, SQL, or visitor data for API failures.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CourseInquiryDashboard.ErrorHandling");
        logger.LogError("Unhandled exception of type {ErrorType}", ex.GetType().FullName ?? "Unknown");
        if (context.Request.Path.StartsWithSegments("/api/inquiries"))
            context.RequestServices.GetRequiredService<InquiryMetrics>().AddIntake("serverError");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
    }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// C7: compiled island assets live under the dedicated wwwroot/app subfolder;
// the shell and API stay reachable without the Vite dev server.
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/dashboard"));
app.MapControllers();
app.MapHealthChecks("/health");

// Dev-only scenario seeding for the dashboard (empty / full / pagination states).
// Enabled automatically in Development, or in any environment by explicitly
// setting DevTools:ScenarioSeeding=true — the endpoints wipe and rewrite data,
// so they must stay off in production unless deliberately opted in.
if (DevToolsOptions.ScenarioSeedingEnabled(app.Environment, app.Configuration))
{
    app.MapScenarioEndpoints();
}

// Dev-only runtime CRM outcome selection and safe result inspection.
if (DevToolsOptions.CrmSimulationEnabled(app.Environment, app.Configuration))
{
    app.MapCrmSimulationEndpoints();
}

await app.RunAsync();

public partial class Program;
