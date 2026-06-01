namespace NetCoreBackend.NArchitecture.Core.Persistence.Paging;

// Mirror of IPaginate<T> metadata for DTO/response mapping. Property set kept aligned
// (including From) so AutoMapper / manual copy between Paginate<T> and any
// BasePageableModel-derived response preserves every paging field.
public abstract class BasePageableModel
{
    public int From { get; set; }
    public int Index { get; set; }
    public int Size { get; set; }
    public int Count { get; set; }
    public int Pages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}
