using TutorSphere.Application.Common;

namespace TutorSphere.UnitTests;

public class ActivationKeyFormatTests
{
    [Fact]
    public void Generate_matches_tutor_month_token_day_guid_format()
    {
        var utc = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var key = ActivationKeyFormat.Generate(utc);

        Assert.True(ActivationKeyFormat.IsValid(key));
        Assert.StartsWith("TUTOR-08-", key);
        Assert.Equal(15, int.Parse(key.Split('-')[3]));
        Assert.Equal(5, key.Split('-').Length);
        Assert.Equal(5, key.Split('-')[2].Length);
        Assert.Equal(32, key.Split('-')[4].Length);
    }

    [Fact]
    public void Generate_produces_unique_guid_segments()
    {
        var a = ActivationKeyFormat.Generate();
        var b = ActivationKeyFormat.Generate();
        Assert.NotEqual(a, b);
        Assert.NotEqual(a.Split('-')[^1], b.Split('-')[^1]);
    }

    [Theory]
    [InlineData("TS-ABCD1234")]
    [InlineData("RENTREE2026")]
    [InlineData("TUTOR-13-K7P2M-15-A1B2C3D4E5F64789ABCDEF01234567")]
    [InlineData("TUTOR-08-K7P2-15-A1B2C3D4E5F64789ABCDEF01234567")]
    public void IsValid_rejects_legacy_or_malformed_keys(string code)
    {
        Assert.False(ActivationKeyFormat.IsValid(code));
    }

    [Fact]
    public void EnsureFormat_accepts_generated_keys()
    {
        ActivationKeyFormat.EnsureFormat(ActivationKeyFormat.Generate());
    }
}
