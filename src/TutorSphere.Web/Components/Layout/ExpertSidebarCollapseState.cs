namespace TutorSphere.Web.Components.Layout;

/// <summary>État repli des sections sidebar Expert (durée de vie = circuit Blazor).</summary>
public static class ExpertSidebarCollapseState
{
    private static readonly Dictionary<string, bool> States = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string key, out bool open) => States.TryGetValue(key, out open);

    public static void Set(string key, bool open) => States[key] = open;
}
