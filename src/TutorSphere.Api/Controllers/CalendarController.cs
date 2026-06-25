using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarController(ICalendarService calendarService) => _calendarService = calendarService;

    [HttpGet("view")]
    public async Task<ActionResult<CalendarViewDto>> GetView(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] CalendarView? view,
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        try
        {
            if (view.HasValue && date.HasValue)
                return Ok(await _calendarService.GetViewByCalendarViewAsync(view.Value, date.Value, ct));

            if (!start.HasValue || !end.HasValue)
                return BadRequest(new { error = "Spécifiez start/end ou view/date." });

            return Ok(await _calendarService.GetViewAsync(start.Value, end.Value, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("unavailabilities")]
    public async Task<ActionResult<IReadOnlyList<UnavailabilityDto>>> ListUnavailabilities(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.GetUnavailabilitiesAsync(start, end, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("unavailabilities")]
    public async Task<ActionResult<UnavailabilityDto>> CreateUnavailability(
        [FromBody] CreateUnavailabilityRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await _calendarService.CreateUnavailabilityAsync(request, ct);
            return CreatedAtAction(nameof(ListUnavailabilities), new { start = request.StartTime, end = request.EndTime }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("unavailabilities/{id:guid}")]
    public async Task<ActionResult<UnavailabilityDto>> UpdateUnavailability(
        Guid id,
        [FromBody] UpdateUnavailabilityRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.UpdateUnavailabilityAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("unavailabilities/{id:guid}")]
    public async Task<IActionResult> DeleteUnavailability(Guid id, CancellationToken ct)
    {
        try
        {
            await _calendarService.DeleteUnavailabilityAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("holidays")]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> ListHolidays(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.GetHolidaysAsync(start, end, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("holidays")]
    public async Task<ActionResult<HolidayDto>> CreateHoliday(
        [FromBody] CreateHolidayRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await _calendarService.CreateHolidayAsync(request, ct);
            return CreatedAtAction(nameof(ListHolidays), new { start = request.StartDate, end = request.EndDate }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("holidays/{id:guid}")]
    public async Task<ActionResult<HolidayDto>> UpdateHoliday(
        Guid id,
        [FromBody] UpdateHolidayRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.UpdateHolidayAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("holidays/{id:guid}")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        try
        {
            await _calendarService.DeleteHolidayAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("vacations")]
    public async Task<ActionResult<IReadOnlyList<VacationDto>>> ListVacations(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.GetVacationsAsync(start, end, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("vacations")]
    public async Task<ActionResult<VacationDto>> CreateVacation(
        [FromBody] CreateVacationRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await _calendarService.CreateVacationAsync(request, ct);
            return CreatedAtAction(nameof(ListVacations), new { start = request.StartDate, end = request.EndDate }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("vacations/{id:guid}")]
    public async Task<ActionResult<VacationDto>> UpdateVacation(
        Guid id,
        [FromBody] UpdateVacationRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _calendarService.UpdateVacationAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("vacations/{id:guid}")]
    public async Task<IActionResult> DeleteVacation(Guid id, CancellationToken ct)
    {
        try
        {
            await _calendarService.DeleteVacationAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
