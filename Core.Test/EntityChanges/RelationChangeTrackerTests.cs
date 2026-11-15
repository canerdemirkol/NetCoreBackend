using NetCoreBackend.NArchitecture.Core.EntityChanges.Relations;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.Test.EntityChanges;

/// <summary>
/// Bulk relation writes (delete-all then add, ExecuteDelete) are invisible to the change tracker. The tracker reads
/// the set of names before and after the write and reports one change when the sets differ.
/// </summary>
public sealed class RelationChangeTrackerTests
{
    [Fact]
    public async Task AChangedSet_IsOneChange_WithTheSortedOldAndNewNames()
    {
        List<string> members = ["Users.Read"];

        (int result, PropertyChange? change) = await RelationChangeTracker.TrackAsync(
            () => Task.FromResult<IReadOnlyCollection<string>>(members.ToList()),
            () => { members = ["Users.Read", "Roles.Admin"]; return Task.FromResult(7); },
            "OperationClaims");

        Assert.Equal(7, result);
        Assert.Equal(new PropertyChange("OperationClaims", "Users.Read", "Roles.Admin, Users.Read"), change);
    }

    [Fact]
    public async Task AnUnchangedSet_IsNoChange()
    {
        (bool _, PropertyChange? change) = await RelationChangeTracker.TrackAsync(
            () => Task.FromResult<IReadOnlyCollection<string>>(["Admin"]),
            () => Task.FromResult(true),
            "Roles");

        Assert.Null(change);
    }

    [Fact]
    public async Task SetsThatDifferOnlyPastTheCut_StillCount()
    {
        List<string> before = Enumerable.Range(0, 200).Select(i => $"Claim.{i:D3}").ToList();
        List<string> current = before;

        (bool _, PropertyChange? change) = await RelationChangeTracker.TrackAsync(
            () => Task.FromResult<IReadOnlyCollection<string>>(current.ToList()),
            () => { current = [.. before, "Claim.999"]; return Task.FromResult(true); },
            "OperationClaims");

        Assert.NotNull(change);
    }

    [Fact]
    public async Task AFailedWrite_IsNoChange_AndTheErrorGoesOn()
    {
        int reads = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => RelationChangeTracker.TrackAsync<bool>(
            () => { reads++; return Task.FromResult<IReadOnlyCollection<string>>(["Admin"]); },
            () => throw new InvalidOperationException("write failed"),
            "Roles"));

        Assert.Equal(1, reads);
    }
}
