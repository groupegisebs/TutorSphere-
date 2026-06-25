using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class LessonsController : ControllerBase
{
    private readonly ILessonService _lessonService;

    public LessonsController(ILessonService lessonService) => _lessonService = lessonService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LessonDto>>> List(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] CalendarView? view,
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        try
        {
            if (view.HasValue && date.HasValue)
                return Ok(await _lessonService.GetByViewAsync(view.Value, date.Value, ct));

            if (!start.HasValue || !end.HasValue)
                return BadRequest(new { error = "Spécifiez start/end ou view/date." });

            return Ok(await _lessonService.GetByDateRangeAsync(start.Value, end.Value, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonDto>> GetById(Guid id, CancellationToken ct)
    {
        var lesson = await _lessonService.GetByIdAsync(id, ct);
        return lesson is null ? NotFound() : Ok(lesson);
    }

    [HttpPost]
    public async Task<ActionResult<LessonDto>> Create([FromBody] CreateLessonRequest request, CancellationToken ct)
    {
        try
        {
            var lesson = await _lessonService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, lesson);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LessonDto>> Update(Guid id, [FromBody] UpdateLessonRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.UpdateAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _lessonService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
