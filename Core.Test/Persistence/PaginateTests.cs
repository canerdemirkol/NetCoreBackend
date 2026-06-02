using NetCoreBackend.NArchitecture.Core.Persistence.Paging;

namespace NetCoreBackend.NArchitecture.Core.Test.Persistence;

public sealed class PaginateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveSize_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Paginate<int>(Enumerable.Range(1, 5), index: 0, size: size, from: 0));
    }

    [Fact]
    public void Ctor_FromGreaterThanIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Paginate<int>(Enumerable.Range(1, 5), index: 0, size: 10, from: 1));
    }

    [Fact]
    public void Ctor_ValidArgs_ComputesPagingMetadata()
    {
        Paginate<int> page = new(Enumerable.Range(1, 25), index: 1, size: 10, from: 0);

        Assert.Equal(25, page.Count);
        Assert.Equal(3, page.Pages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal(new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }, page.Items);
    }
}
