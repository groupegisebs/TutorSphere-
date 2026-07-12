using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.Services;

namespace TutorSphere.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IHomeworkService, HomeworkService>();
        services.AddScoped<ILessonReportService, LessonReportService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IParentService, ParentService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISubscriptionOfferingService, SubscriptionOfferingService>();
        services.AddScoped<IStudentSubscriptionService, StudentSubscriptionService>();
        services.AddScoped<ISubscriptionLessonScheduler, SubscriptionLessonScheduler>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        return services;
    }
}
