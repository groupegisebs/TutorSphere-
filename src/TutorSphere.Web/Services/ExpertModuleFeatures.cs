using TutorSphere.Application.Options;
using TutorSphere.Application.Services;

namespace TutorSphere.Web.Services;

/// <summary>Facade UI pour les flags Expert (injectée dans la sidebar).</summary>
public class ExpertModuleFeatures(IExpertModuleFeatureService features)
{
    public bool IsEnabled(string key) => features.IsEnabled(key);
    public bool IsFrozen(string key) => features.IsFrozen(key);

    public bool Dashboard => IsEnabled(ExpertModuleKeys.Dashboard);
    public bool Approvals => IsEnabled(ExpertModuleKeys.Approvals);
    public bool Teachers => IsEnabled(ExpertModuleKeys.Teachers);
    public bool Disciplines => IsEnabled(ExpertModuleKeys.Disciplines);
    public bool Offers => IsEnabled(ExpertModuleKeys.Offers);
    public bool Members => IsEnabled(ExpertModuleKeys.Members);
    public bool Admissions => IsEnabled(ExpertModuleKeys.Admissions);
    public bool AdminChat => IsEnabled(ExpertModuleKeys.AdminChat);
    public bool GroupAdmin => IsEnabled(ExpertModuleKeys.GroupAdmin);
    public bool Meetings => IsEnabled(ExpertModuleKeys.Meetings);
}
