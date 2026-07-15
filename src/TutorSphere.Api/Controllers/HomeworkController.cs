using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant}")]
public class HomeworkController : ControllerBase
{
    private readonly IHomeworkService _homeworkService;

    public HomeworkController(IHomeworkService homeworkService) => _homeworkService = homeworkService;

    [HttpPost]
    public async Task<ActionResult<HomeworkDto>> Create([FromBody] CreateHomeworkRequest request, CancellationToken ct)
    {
        try
        {
            var homework = await _homeworkService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = homework.Id }, homework);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<HomeworkDto>>> CreateBatch(
        [FromBody] CreateHomeworkBatchRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _homeworkService.CreateBatchAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HomeworkDto>>> GetMine(CancellationToken ct) =>
        Ok(await _homeworkService.GetForCurrentTenantAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HomeworkDto>> GetById(Guid id, CancellationToken ct)
    {
        var homework = await _homeworkService.GetByIdAsync(id, ct);
        return homework is null ? NotFound() : Ok(homework);
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<HomeworkDto>>> GetByStudent(Guid studentId, CancellationToken ct)
    {
        return Ok(await _homeworkService.GetByStudentAsync(studentId, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HomeworkDto>> Update(Guid id, [FromBody] UpdateHomeworkRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _homeworkService.UpdateAsync(id, request, ct));
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
            await _homeworkService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<HomeworkDto>> Submit(Guid id, [FromBody] SubmitHomeworkRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _homeworkService.SubmitAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/grade")]
    public async Task<ActionResult<HomeworkDto>> Grade(Guid id, [FromBody] GradeHomeworkRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _homeworkService.GradeAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
