using System.Text.Json.Nodes;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;
using CourseInquiryDashboard.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new StatusJsonConverter()));
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.MapType<Status>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Enum = StatusNames.All.Select(name => (JsonNode)JsonValue.Create(name)!).ToList(),
    }));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddSingleton<ICrmClient, SimulatedCrmClient>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("The database connection is not configured.")));

// C6 privacy policy: EF Core attaches raw provider exceptions (with SQL and parameters)
// to Error-level entries; keep the whole EF surface out of sinks below Critical.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);

var app = builder.Build();

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
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
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
await app.RunAsync();

public partial class Program;
