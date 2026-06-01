using System.ComponentModel.DataAnnotations;

namespace NetCoreBackend.NArchitecture.Core.Application.Requests;

// Standard paging query string. Attributes guard against negative indexes (EF Core's Skip
// throws on negative values) and DoS via huge result sets (e.g. ?PageSize=10000000).
// The upper bound on PageSize (1000) is conservative; raise per use case if needed.
public class PageRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "PageIndex must be zero or positive.")]
    public int PageIndex { get; set; }

    [Range(1, 1000, ErrorMessage = "PageSize must be between 1 and 1000.")]
    public int PageSize { get; set; } = 10;
}
