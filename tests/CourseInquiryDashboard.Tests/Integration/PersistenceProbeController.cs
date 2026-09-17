using CourseInquiryDashboard.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// Test-only controller exposing persisted values over the real MVC pipeline so persistence tests can
/// observe HTTP JSON (e.g. the UTC "Z" representation in IT-DATA-005) without depending on the
/// production response DTOs. Registered per test host through
/// <c>services.AddControllers().AddApplicationPart(...)</c>.
/// </summary>
[ApiController]
[Route("test-support/persistence")]
public sealed class PersistenceProbeController(AppDbContext db) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok" });

    [HttpGet("inquiries/{id:int}")]
    public async Task<IActionResult> GetInquiry(int id)
    {
        var inquiry = await db.CourseInquiries.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id);

        return inquiry is null
            ? NotFound()
            : Ok(new
            {
                id = inquiry.Id,
                status = inquiry.Status.ToString(),
                createdDate = inquiry.CreatedDate,
                updatedDate = inquiry.UpdatedDate,
            });
    }
}
