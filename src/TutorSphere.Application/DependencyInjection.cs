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
        return services;
    }
}
