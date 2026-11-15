using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Relations;

/// <summary>
/// Bulk relation writes (delete-all then add, ExecuteDelete) never pass the change tracker, so the collector cannot
/// see them. Wrap such a write here: the set of member names is read before and after it and compared as sets; one
/// change comes back when they differ, for the consumer to write on the owner (a pseudo property such as "Roles").
/// A failed write reports nothing and its error goes on. The read, the write and the consumer's row are atomic only
/// inside the request's transaction (e.g. a TransactionScope behavior); without one, a write that half fails is not
/// recorded.
/// </summary>
public static class RelationChangeTracker
{
    public static async Task<(TResult Result, PropertyChange? Change)> TrackAsync<TResult>(
        Func<Task<IReadOnlyCollection<string>>> readSet,
        Func<Task<TResult>> write,
        string property)
    {
        IReadOnlyCollection<string> before = await readSet();
        TResult result = await write();
        IReadOnlyCollection<string> after = await readSet();

        // Compared as sets, not as the stored text: two long sets may differ only past the 512-character cut.
        if (new HashSet<string>(before, StringComparer.Ordinal).SetEquals(after))
            return (result, null);
        return (result, new PropertyChange(property, EntityChangeValues.SetText(before), EntityChangeValues.SetText(after)));
    }
}
