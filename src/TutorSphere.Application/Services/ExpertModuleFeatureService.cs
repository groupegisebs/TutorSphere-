using Microsoft.Extensions.Options;
using TutorSphere.Application.Options;

namespace TutorSphere.Application.Services;

public interface IExpertModuleFeatureService
{
    bool IsEnabled(string moduleKey);
    bool IsFrozen(string moduleKey);
    ExpertModuleFeatureOptions Snapshot { get; }
}

public class ExpertModuleFeatureService(IOptions<ExpertModuleFeatureOptions> options) : IExpertModuleFeatureService
{
    public ExpertModuleFeatureOptions Snapshot => options.Value;
    public bool IsEnabled(string moduleKey) => options.Value.IsEnabled(moduleKey);
    public bool IsFrozen(string moduleKey) => options.Value.IsFrozen(moduleKey);
}
