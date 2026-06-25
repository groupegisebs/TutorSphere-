using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace TutorSphere.Web.Services;

public static class LocalizationSetup
{
    public const string ResourcesPath = "Resources";

    public static RequestLocalizationOptions CreateRequestLocalizationOptions()
    {
        var cultures = Application.Common.SupportedLanguageCodes.Cultures;

        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Application.Common.SupportedLanguageCodes.Default),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
            ApplyCurrentCultureToResponseHeaders = true
        };
    }
}
