using TutorSphere.Application.Services;
using TutorSphere.Domain.Entities;

namespace TutorSphere.UnitTests;

/// <summary>
/// Le pays ne réserve plus de territoire : il peut désigner zéro, un ou plusieurs groupes.
/// Ces cas décident où part une candidature, donc quel groupe la voit et peut l'approuver.
/// </summary>
public class ExpertGroupReviewRoutingTests
{
    private static ExpertGroup Group(
        string name,
        string? country = null,
        bool active = true,
        bool isDefault = false,
        bool international = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CountryCode = country,
            IsActive = active,
            IsDefaultReviewGroup = isDefault,
            IsInternational = international
        };

    [Fact]
    public void SeulGroupeDuPays_RecoitLaCandidature()
    {
        var db = new MemoryAppDb();
        var cameroun = Group("Cameroun", "CM");
        db.ExpertGroupsList.Add(cameroun);
        db.ExpertGroupsList.Add(Group("Défaut", isDefault: true));

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup("cm");

        Assert.Equal(cameroun.Id, resolved?.Id);
    }

    [Fact]
    public void PaysRevendiqueParPlusieursGroupes_BasculeSurLeGroupeParDefaut()
    {
        var db = new MemoryAppDb();
        db.ExpertGroupsList.Add(Group("Cameroun A", "CM"));
        db.ExpertGroupsList.Add(Group("Cameroun B", "CM"));
        var fallback = Group("Défaut", isDefault: true);
        db.ExpertGroupsList.Add(fallback);

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup("CM");

        Assert.Equal(fallback.Id, resolved?.Id);
    }

    [Fact]
    public void AucunGroupePourLePays_BasculeSurLeGroupeParDefaut()
    {
        var db = new MemoryAppDb();
        db.ExpertGroupsList.Add(Group("Cameroun", "CM"));
        var fallback = Group("Défaut", isDefault: true);
        db.ExpertGroupsList.Add(fallback);

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup("FR");

        Assert.Equal(fallback.Id, resolved?.Id);
    }

    [Fact]
    public void GroupeParDefautInactif_NeRecoitRien()
    {
        var db = new MemoryAppDb();
        db.ExpertGroupsList.Add(Group("Défaut suspendu", isDefault: true, active: false));
        db.ExpertGroupsList.Add(Group("Autre A"));
        db.ExpertGroupsList.Add(Group("Autre B"));

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup("FR");

        Assert.Null(resolved);
    }

    [Fact]
    public void UnSeulGroupeActif_RecoitToutSansDesignation()
    {
        var db = new MemoryAppDb();
        var seul = Group("Unique");
        db.ExpertGroupsList.Add(seul);
        db.ExpertGroupsList.Add(Group("Archivé", active: false));

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup(null);

        Assert.Equal(seul.Id, resolved?.Id);
    }

    [Fact]
    public void GroupeInactifDuPays_EstIgnore()
    {
        var db = new MemoryAppDb();
        db.ExpertGroupsList.Add(Group("Cameroun suspendu", "CM", active: false));
        var fallback = Group("Défaut", isDefault: true);
        db.ExpertGroupsList.Add(fallback);

        var resolved = new ExpertGroupService(db).ResolveReviewerGroup("CM");

        Assert.Equal(fallback.Id, resolved?.Id);
    }
}
