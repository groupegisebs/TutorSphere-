using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application.DTOs.Tenants;
using TutorSphere.Application.Services;

namespace TutorSphere.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}
