using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseInquiryDashboard.Controllers;

/// <summary>
/// REST API controller exposing the five inquiry operations (ADR-0002, ADR-0005). Thin
/// controller: handles HTTP semantics, routing, and status-code mapping, delegating all
/// business rules and database access to <see cref="IInquiryService"/>.
/// </summary>
[ApiController]
[Route("api/inquiries")]
public sealed class InquiriesController(IInquiryService inquiryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInquiryDto request, CancellationToken cancellationToken)
    {
        var created = await inquiryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(InquiryPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InquiryPageResponse>> List(
        [FromQuery] ListInquiriesQueryDto request, CancellationToken cancellationToken)
        => await inquiryService.ListAsync(request.ToQuery(), cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound(); // zero/negative integers cannot identify a stored row (C3)

        return await inquiryService.GetByIdAsync(id, cancellationToken) is { } inquiry
            ? Ok(inquiry)
            : NotFound();
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateStatusDto request, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        return await inquiryService.UpdateStatusAsync(id, request.Status!.Value, cancellationToken) is { } updated
            ? Ok(updated)
            : NotFound();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        return await inquiryService.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
