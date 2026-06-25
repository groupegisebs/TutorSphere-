using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.LessonReports;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant}")]
public class LessonReportsController : ControllerBase
{
    private readonly ILessonReportService _lessonReportService;

    public LessonReportsController(ILessonReportService lessonReportService) =>
        _lessonReportService = lessonReportService;

    [HttpPost]
    public async Task<ActionResult<LessonReportDto>> Create([FromBody] CreateLessonReportRequest request, CancellationToken ct)
    {
        try
        {
            var report = await _lessonReportService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = report.Id }, report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonReportDto>> GetById(Guid id, CancellationToken ct)
    {
        var report = await _lessonReportService.GetByIdAsync(id, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("lesson/{lessonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<LessonReportDto>>> GetByLesson(Guid lessonId, CancellationToken ct)
    {
        return Ok(await _lessonReportService.GetByLessonAsync(lessonId, ct));
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<LessonReportDto>>> GetByStudent(Guid studentId, CancellationToken ct)
    {
        return Ok(await _lessonReportService.GetByStudentAsync(studentId, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LessonReportDto>> Update(Guid id, [FromBody] UpdateLessonReportRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonReportService.UpdateAsync(id, request, ct));
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
            await _lessonReportService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/send-to-parent")]
    public async Task<ActionResult<LessonReportDto>> MarkSentToParent(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _lessonReportService.MarkSentToParentAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
