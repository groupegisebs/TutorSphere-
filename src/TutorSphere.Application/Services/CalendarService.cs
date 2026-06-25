using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface ICalendarService
{
    Task<CalendarViewDto> GetViewAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<CalendarViewDto> GetViewByCalendarViewAsync(CalendarView view, DateTime date, CancellationToken ct = default);

    Task<IReadOnlyList<UnavailabilityDto>> GetUnavailabilitiesAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<UnavailabilityDto> CreateUnavailabilityAsync(CreateUnavailabilityRequest request, CancellationToken ct = default);
    Task<UnavailabilityDto> UpdateUnavailabilityAsync(Guid id, UpdateUnavailabilityRequest request, CancellationToken ct = default);
    Task DeleteUnavailabilityAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default);
    Task<HolidayDto> UpdateHolidayAsync(Guid id, UpdateHolidayRequest request, CancellationToken ct = default);
    Task DeleteHolidayAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<VacationDto>> GetVacationsAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<VacationDto> CreateVacationAsync(CreateVacationRequest request, CancellationToken ct = default);
    Task<VacationDto> UpdateVacationAsync(Guid id, UpdateVacationRequest request, CancellationToken ct = default);
    Task DeleteVacationAsync(Guid id, CancellationToken ct = default);
}

public class CalendarService : ICalendarService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILessonService _lessonService;

    public CalendarService(IApplicationDbContext db, ITenantContext tenantContext, ILessonService lessonService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lessonService = lessonService;
    }

    public async Task<CalendarViewDto> GetViewAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);

        var lessons = await _lessonService.GetByDateRangeAsync(start, end, ct);
        var unavailabilities = await GetUnavailabilitiesAsync(start, end, ct);
        var holidays = await GetHolidaysAsync(start, end, ct);
        var vacations = await GetVacationsAsync(start, end, ct);

        return new CalendarViewDto(start, end, lessons, unavailabilities, holidays, vacations);
    }

    public Task<CalendarViewDto> GetViewByCalendarViewAsync(CalendarView view, DateTime date, CancellationToken ct = default)
    {
        var (start, end) = CalendarRangeHelper.GetViewRange(view, date);
        return GetViewAsync(start, end, ct);
    }

    public Task<IReadOnlyList<UnavailabilityDto>> GetUnavailabilitiesAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);

        var items = _db.Unavailabilities
            .Where(u => u.StartTime < end && u.EndTime > start)
            .OrderBy(u => u.StartTime)
            .ToList()
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<UnavailabilityDto>>(items);
    }

    public async Task<UnavailabilityDto> CreateUnavailabilityAsync(CreateUnavailabilityRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var entity = new Unavailability
        {
            TenantId = RequireTenantId(),
            Reason = request.Reason?.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<UnavailabilityDto> UpdateUnavailabilityAsync(Guid id, UpdateUnavailabilityRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var entity = _db.Unavailabilities.FirstOrDefault(u => u.Id == id)
            ?? throw new InvalidOperationException("Indisponibilité introuvable.");

        entity.Reason = request.Reason?.Trim();
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task DeleteUnavailabilityAsync(Guid id, CancellationToken ct = default)
    {
        var entity = _db.Unavailabilities.FirstOrDefault(u => u.Id == id)
            ?? throw new InvalidOperationException("Indisponibilité introuvable.");

        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);

        var items = _db.Holidays
            .Where(h => h.StartDate < end && h.EndDate > start)
            .OrderBy(h => h.StartDate)
            .ToList()
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<HolidayDto>>(items);
    }

    public async Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var entity = new Holiday
        {
            TenantId = RequireTenantId(),
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<HolidayDto> UpdateHolidayAsync(Guid id, UpdateHolidayRequest request, CancellationToken ct = default)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var entity = _db.Holidays.FirstOrDefault(h => h.Id == id)
            ?? throw new InvalidOperationException("Jour férié introuvable.");

        entity.Name = request.Name.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct = default)
    {
        var entity = _db.Holidays.FirstOrDefault(h => h.Id == id)
            ?? throw new InvalidOperationException("Jour férié introuvable.");

        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<VacationDto>> GetVacationsAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);

        var items = _db.Vacations
            .Where(v => v.StartDate < end && v.EndDate > start)
            .OrderBy(v => v.StartDate)
            .ToList()
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<VacationDto>>(items);
    }

    public async Task<VacationDto> CreateVacationAsync(CreateVacationRequest request, CancellationToken ct = default)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var entity = new Vacation
        {
            TenantId = RequireTenantId(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<VacationDto> UpdateVacationAsync(Guid id, UpdateVacationRequest request, CancellationToken ct = default)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var entity = _db.Vacations.FirstOrDefault(v => v.Id == id)
            ?? throw new InvalidOperationException("Vacances introuvables.");

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task DeleteVacationAsync(Guid id, CancellationToken ct = default)
    {
        var entity = _db.Vacations.FirstOrDefault(v => v.Id == id)
            ?? throw new InvalidOperationException("Vacances introuvables.");

        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");

        return _tenantContext.TenantId.Value;
    }

    private static void ValidateTimeRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new InvalidOperationException("L'heure de fin doit être postérieure à l'heure de début.");
    }

    private static void ValidateDateRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new InvalidOperationException("La date de fin doit être postérieure à la date de début.");
    }

    private static UnavailabilityDto MapToDto(Unavailability entity) => new(
        entity.Id,
        entity.Reason,
        entity.StartTime,
        entity.EndTime,
        entity.CreatedAt,
        entity.UpdatedAt);

    private static HolidayDto MapToDto(Holiday entity) => new(
        entity.Id,
        entity.Name,
        entity.StartDate,
        entity.EndDate,
        entity.CreatedAt,
        entity.UpdatedAt);

    private static VacationDto MapToDto(Vacation entity) => new(
        entity.Id,
        entity.Title,
        entity.Description,
        entity.StartDate,
        entity.EndDate,
        entity.CreatedAt,
        entity.UpdatedAt);
}
