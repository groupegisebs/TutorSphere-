using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.Services;

public sealed class ConfigurationAppUrlProvider : IAppUrlProvider
{
    public ConfigurationAppUrlProvider(IConfiguration configuration)
    {
        WebBaseUrl = (configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
    }

    public string WebBaseUrl { get; }
}
