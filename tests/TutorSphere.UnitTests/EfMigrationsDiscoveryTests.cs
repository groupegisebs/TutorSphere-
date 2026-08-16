using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using TutorSphere.Infrastructure.Migrations;

namespace TutorSphere.UnitTests;

public class EfMigrationsDiscoveryTests
{
    [Theory]
    [InlineData("20260816010000_AddTeacherLicenseWithholding")]
    [InlineData("20260816020000_AddLicenseAutoRenewAtSource")]
    [InlineData("20260816030000_WidenActivationKeyCode")]
    [InlineData("20260816040000_AddLessonCoverageAssignments")]
    [InlineData("20260816050000_AddDocumentMetadata")]
    [InlineData("20260816060000_AddTutorPayoutGroupInvoice")]
    public void License_schema_migrations_are_discoverable_by_ef(string migrationId)
    {
        var ids = typeof(AddLicenseAutoRenewAtSource).Assembly
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id is not null)
            .ToHashSet();

        Assert.Contains(migrationId, ids);
    }
}
