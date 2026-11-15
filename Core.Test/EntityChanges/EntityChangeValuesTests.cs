using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.Test.EntityChanges;

/// <summary>
/// How a change log value is written: language-neutral text (the screen formats it), cut at 512 characters; a
/// relation is a sorted list of names; a secret is a flag with no value. The JSON shape is a contract with the rows
/// already stored, so it is pinned byte for byte.
/// </summary>
public sealed class EntityChangeValuesTests
{
    private enum Plan { Free, Pro }

    [Fact]
    public void Values_AreLanguageNeutralText()
    {
        var id = Guid.Parse("6f1c2a52-3b2f-4b8a-9d0e-0a1b2c3d4e5f");

        Assert.Null(EntityChangeValues.Format(null));
        Assert.Equal("Ayşe", EntityChangeValues.Format("Ayşe"));
        Assert.Equal("true", EntityChangeValues.Format(true));
        Assert.Equal("false", EntityChangeValues.Format(false));
        Assert.Equal("Pro", EntityChangeValues.Format(Plan.Pro));
        Assert.Equal("6f1c2a52-3b2f-4b8a-9d0e-0a1b2c3d4e5f", EntityChangeValues.Format(id));
        Assert.Equal("12", EntityChangeValues.Format(12));
        Assert.Equal("1250.50", EntityChangeValues.Format(1250.50m));
    }

    [Fact]
    public void ATime_IsIso8601Utc()
    {
        var time = new DateTime(2026, 11, 15, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal("2026-11-15T09:30:00.0000000Z", EntityChangeValues.Format(time));
        Assert.Equal("2026-11-15T09:30:00.0000000Z", EntityChangeValues.Format(DateTime.SpecifyKind(time, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ALongValue_IsCutAt512_WithAnEllipsis()
    {
        string cut = EntityChangeValues.Format(new string('a', 600))!;

        Assert.Equal(EntityChangeValues.MaxValueLength, cut.Length);
        Assert.EndsWith("…", cut);
        Assert.Equal(new string('b', 512), EntityChangeValues.Format(new string('b', 512)));
    }

    [Fact]
    public void ARelation_IsTheSortedNames()
    {
        Assert.Equal("Roles.Admin, Users.Read", EntityChangeValues.SetText(["Users.Read", "Roles.Admin"]));
        Assert.Null(EntityChangeValues.SetText([]));
        Assert.Equal(EntityChangeValues.MaxValueLength, EntityChangeValues.SetText(Enumerable.Range(0, 200).Select(i => $"Claim.{i:D3}"))!.Length);
    }

    [Fact]
    public void TheJson_IsTheStoredContract()
    {
        PropertyChange[] changes =
        [
            new("FirstName", "Ali", "Veli"),
            new("LastName", null, "Değişiklik"),
            new("PhoneNumber", "+905550000086", null),
            new("Value", null, null, Masked: true),
        ];

        string json = EntityChangeValues.Serialize(changes);

        Assert.Equal(
            """[{"p":"FirstName","o":"Ali","n":"Veli"},{"p":"LastName","n":"Değişiklik"},{"p":"PhoneNumber","o":"\u002B905550000086"},{"p":"Value","m":true}]""",
            json);
        Assert.Equal(changes, EntityChangeValues.Deserialize(json));
    }

    [Fact]
    public void TheStoredNumbers_NeverMove()
    {
        Assert.Equal([0, 1, 2], Enum.GetValues<EntityChangeType>().Select(v => (int)v));
        Assert.Equal(["Created", "Updated", "Deleted"], Enum.GetNames<EntityChangeType>());
        Assert.Equal([0, 1, 2], Enum.GetValues<EntityChangeActorType>().Select(v => (int)v));
        Assert.Equal(["Anonymous", "User", "PlatformAdmin"], Enum.GetNames<EntityChangeActorType>());
        Assert.Equal([0, 1, 2], Enum.GetValues<EntityChangeImpersonationKind>().Select(v => (int)v));
        Assert.Equal(["None", "User", "Tenant"], Enum.GetNames<EntityChangeImpersonationKind>());
    }
}
