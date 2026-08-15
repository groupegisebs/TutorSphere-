using TutorSphere.Application.Services;

namespace TutorSphere.Api.BackgroundServices;

public class MeetingReminderService(IServiceProvider services, ILogger<MeetingReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var meetings = scope.ServiceProvider.GetRequiredService<IExpertMeetingService>();
                await meetings.ProcessRemindersAndRetriesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur lors des rappels de réunion.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
