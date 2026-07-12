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
    public async Task<ActionResult<IReadOnlyList<LessonDto>>> Create([FromBody] CreateLessonRequest request, CancellationToken ct)
    {
        try
        {
            var lessons = await _lessonService.CreateAsync(request, ct);
            if (lessons.Count == 0)
                return BadRequest(new { error = "Aucune séance créée." });

            return CreatedAtAction(nameof(GetById), new { id = lessons[0].Id }, lessons);
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

    /// <summary>Annulation : ≥24h = non comptée ; sinon validée (comptée).</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<LessonDto>> Cancel(Guid id, [FromBody] CancelLessonRequest? request, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.CancelAsync(id, request ?? new CancelLessonRequest(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Moniteur absent → séance non comptée, moniteur imputable (replanifier / rembourser).</summary>
    [HttpPost("{id:guid}/tutor-no-show")]
    public async Task<ActionResult<LessonDto>> TutorNoShow(Guid id, [FromBody] MarkTutorNoShowRequest? request, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.MarkTutorNoShowAsync(id, request ?? new MarkTutorNoShowRequest(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resolve-liability")]
    public async Task<ActionResult<LessonDto>> ResolveLiability(
        Guid id,
        [FromBody] ResolveTutorLiabilityRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.ResolveTutorLiabilityAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/attendances")]
    public async Task<ActionResult<IReadOnlyList<LessonAttendanceDto>>> Attendances(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.GetAttendancesAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/attendances")]
    public async Task<ActionResult<LessonAttendanceDto>> SetAttendance(
        Guid id,
        [FromBody] SetAttendanceRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonService.SetAttendanceAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
