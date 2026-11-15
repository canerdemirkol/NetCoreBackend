# Core.EntityChanges

An entity change log for EF Core: which configured record changed, how (created, updated, deleted), by whom, with
the old and new value of each allow-listed property. The change rows are written **in the same `SaveChanges`** as
the change itself, so both commit or neither does.

## Why

Operation logs say *which request ran*; they do not say *what it changed*. Auditors and support ask "who changed this
customer's representative, and from what?". This package answers that without copying a change-capture mechanism into
every project.

## Design choices

| Choice | Why |
|---|---|
| **Allow list per property.** A property is tracked (`Track`: old and new value), masked (`Mask`: only "changed") or absent (never read). There is no "all properties" option. | A column added later (a hash, a token) is never recorded by default. A forgotten property stays invisible instead of leaking. |
| **No hash of masked values.** A masked property is written as `{"p":"Iban","m":true}`; the comparison happens in memory. | A hash of an IBAN or a national id can be brute-forced. |
| **Same save, no separate transaction.** | A change and its history commit together; a failed save leaves no history row behind. |
| **One flat table, JSON properties** `[{"p","o","n","m"}]`. | No joins; the property count is bounded by the allow list; each value is cut at 512 characters. |
| **Configuration classes** (`IEntityChangeConfiguration<T>`), like EF's `IEntityTypeConfiguration<T>`. | Each entity's decision lives in its own file; works for entities whose base class lives in another package (no attributes needed). |
| **No query filter, no actor, no DI.** | Who may read which rows (tenant visibility) and who the actor is are the consumer's rules. |

## Use it in a project

1. Add the package:

   ```bash
   dotnet add package NetCoreBackend.NArchitecture.Core.EntityChanges
   ```

2. Configure the table and add your visibility filter (the migration stays with you):

   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       modelBuilder.ConfigureEntityChanges();   // table "EntityChanges", three indexes
       modelBuilder.Entity<EntityChange>().HasQueryFilter(c => /* your tenant rule */);
   }
   ```

3. Write one configuration class per entity you want to follow:

   ```csharp
   public class InvoiceChangeConfiguration : IEntityChangeConfiguration<Invoice>
   {
       public void Configure(EntityChangeTypeBuilder<Invoice> b) => b
           .HasName("Invoice")                    // stored name; renaming the class never breaks history
           .ScopeBy(i => i.TenantId)              // required: the scope a change belongs to (null = host)
           .IdBy(i => i.Id)                       // required: the record id as stored
           .Track(i => i.Number, i => i.Amount, i => i.Status)
           .Mask(i => i.Iban);                    // "changed", no value, no hash
   }
   ```

   Optional: `FollowWhen(predicate)` (only matching rows are recorded, e.g. not personal preferences) and
   `MaskAllWhen(predicate)` (every property of matching rows is masked, e.g. an encrypted setting).

4. Collect in your `DbContext.SaveChangesAsync`, after your guards and stamping, before the base save:

   ```csharp
   private static readonly EntityChangeModel ChangeModel = EntityChangeModel.FromAssemblies(typeof(MyDbContext).Assembly);

   public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
   {
       // … your guards and CreatedDate/UpdatedDate/TenantId stamping …
       List<EntityChange> rows = ChangeModel.Collect(ChangeTracker.Entries())
           .Select(c => ToRow(c, currentActor))      // your actor: user, actor type, impersonation, correlation id
           .ToList();
       EntityChanges.AddRange(rows);
       try { return await base.SaveChangesAsync(cancellationToken); }
       catch
       {
           foreach (EntityChange row in rows) Entry(row).State = EntityState.Detached;   // no duplicate on a retry
           throw;
       }
   }
   ```

   Also guard the table as append-only (refuse `Modified`/`Deleted` `EntityChange` entries in the same override).

## What it cannot see

Writes that bypass the change tracker are invisible: `ExecuteUpdate`/`ExecuteDelete`, raw SQL, and a synchronous
`SaveChanges` you did not override. For bulk relation writes (delete-all then add), wrap the write:

```csharp
(int result, PropertyChange? change) = await RelationChangeTracker.TrackAsync(
    () => ReadRoleNamesAsync(userId),          // the member names, read before and after
    () => ReplaceRolesAsync(userId, roleIds),
    "Roles");
if (change is not null)
    await AddRowOnTheOwnerAsync("User", userId, change);
```

The read, the write and your row are atomic only inside the request's transaction.

## Model rules (checked when the model is built)

`HasName`, `ScopeBy` and `IdBy` are required; at least one `Track` or `Mask`; a property may appear only once (it is
either tracked or masked); only properties of the entity itself (`x => x.Name`, not `x => x.Address.City`); an entity
type and a name may be configured once. The scan picks public, non-abstract classes with a parameterless constructor.

## Stored values

Language-neutral text: `true`/`false`, enum names, ISO 8601 UTC times (`o`), Guids as `D`, other numbers invariant.
The JSON shape and the enum numbers (`Created 0`, `Updated 1`, `Deleted 2`; actor and impersonation kinds) are a
contract with stored rows; changing them is a breaking change.
