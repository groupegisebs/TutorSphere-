using TutorSphere.Application.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

/// <summary>
/// Devise d'une offre valable dans plusieurs pays. Une erreur ici affiche un prix dans une devise
/// qui n'est pas celle encaissée : 25 000 XAF libellés « $USD » ne se rattrapent pas côté parent.
/// </summary>
public class GroupOfferMultiCountryCurrencyTests
{
    [Theory]
    [InlineData("CA", "CAD")]
    [InlineData("US", "USD")]
    [InlineData("FR", "EUR")]
    [InlineData("CM", "XAF")]
    public void Un_seul_pays_garde_la_devise_de_ce_pays(string country, string expected) =>
        Assert.Equal(expected, GroupOfferCurrencyRules.ResolveCurrencyForCountries([country]));

    [Fact]
    public void Plusieurs_pays_europeens_restent_en_euro() =>
        Assert.Equal("EUR", GroupOfferCurrencyRules.ResolveCurrencyForCountries(["FR", "BE", "DE", "ES"]));

    [Fact]
    public void Plusieurs_pays_de_la_zone_franc_restent_en_xaf() =>
        Assert.Equal("XAF", GroupOfferCurrencyRules.ResolveCurrencyForCountries(["CM", "CI", "SN"]));

    [Fact]
    public void Pays_de_continents_differents_basculent_en_usd() =>
        Assert.Equal("USD", GroupOfferCurrencyRules.ResolveCurrencyForCountries(["FR", "CM", "CA"]));

    [Fact]
    public void Canada_et_usa_basculent_en_usd() =>
        Assert.Equal("USD", GroupOfferCurrencyRules.ResolveCurrencyForCountries(["CA", "US"]));

    [Fact]
    public void Sans_pays_la_devise_est_celle_du_groupe() =>
        Assert.Equal("CAD", GroupOfferCurrencyRules.ResolveCurrencyForCountries([], "CA"));

    [Fact]
    public void Les_doublons_et_la_casse_ne_changent_pas_la_devise() =>
        Assert.Equal("EUR", GroupOfferCurrencyRules.ResolveCurrencyForCountries(["fr", "FR", " fr "]));

    [Fact]
    public void Les_codes_sont_normalises_sans_doublon()
    {
        var codes = GroupOfferCurrencyRules.NormalizeCountryCodes([" ca ", "CA", "us", null, "", "FR"]);
        Assert.Equal(["CA", "US", "FR"], codes);
    }

    [Fact]
    public void Le_csv_des_pays_fait_l_aller_retour()
    {
        var csv = GroupOfferCurrencyRules.ToCountryCsv(["CA", "us", "CA"]);
        Assert.Equal("CA,US", csv);
        Assert.Equal(["CA", "US"], GroupOfferCurrencyRules.ParseCountryCsv(csv));
    }

    [Fact]
    public void Aucun_pays_ne_donne_pas_de_csv() =>
        Assert.Null(GroupOfferCurrencyRules.ToCountryCsv([]));

    [Fact]
    public void Les_niveaux_hors_cycle_sont_ecartes()
    {
        var kept = SchoolLevelCatalog.LevelsWithinCycle(
            ["college", "lycee", "universite"], SchoolCycle.Secondary);
        Assert.Equal(["college", "lycee"], kept);
    }

    [Fact]
    public void Un_niveau_inconnu_est_ignore() =>
        Assert.Empty(SchoolLevelCatalog.NormalizeLevels(["maternelle-2", "inconnu"]));

    [Fact]
    public void Le_cycle_fait_l_aller_retour_en_texte()
    {
        var stored = SchoolLevelCatalog.ToStoredCycle(SchoolCycle.University);
        Assert.Equal(SchoolCycle.University, SchoolLevelCatalog.ParseCycle(stored));
        Assert.Null(SchoolLevelCatalog.ParseCycle("Doctorat"));
    }
}
