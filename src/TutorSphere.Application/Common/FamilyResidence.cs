namespace TutorSphere.Application.Common;

/// <summary>
/// Pays de résidence de la famille : saisi par le parent, repris sur l'enfant
/// (l'enseignant ne voit que ce pays, pas l'adresse du parent) et utilisé
/// pour déterminer les moyens de paiement.
/// </summary>
public static class FamilyResidence
{
    public const string RequiredMessage =
        "Indiquez le pays où vous vous trouvez. Il détermine les moyens de paiement proposés.";

    public static string RequireIso(string? country)
    {
        var code = ParentPaymentMethods.NormalizeIso(country);
        if (code is null)
            throw new InvalidOperationException(RequiredMessage);
        return code;
    }

    public static string? TryIso(string? country) =>
        ParentPaymentMethods.NormalizeIso(country);

    /// <summary>Pays montré pour l'enfant : le sien, sinon celui du parent.</summary>
    public static string? EffectiveChildCountry(string? studentCountry, string? parentCountry) =>
        TryIso(studentCountry) ?? TryIso(parentCountry);
}
