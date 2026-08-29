using Jaberah.Helpers;
using Xunit;

namespace Jaberah.Tests;

public class PagedListTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    [InlineData(100, 25, 4)]
    public void TotalPages_RoundsUp(int count, int pageSize, int expected)
    {
        var page = new PagedList<int>(Array.Empty<int>(), count, 1, pageSize);

        Assert.Equal(expected, page.TotalPages);
    }

    [Fact]
    public void FirstPageOfMany_HasNextButNoPrevious()
    {
        var page = new PagedList<int>(new[] { 1, 2 }, count: 10, page: 1, pageSize: 2);

        Assert.True(page.HasNext);
        Assert.False(page.HasPrevious);
    }

    [Fact]
    public void MiddlePage_HasBothNeighbours()
    {
        var page = new PagedList<int>(new[] { 3, 4 }, count: 10, page: 2, pageSize: 2);

        Assert.True(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Fact]
    public void LastPage_HasPreviousButNoNext()
    {
        var page = new PagedList<int>(new[] { 9, 10 }, count: 10, page: 5, pageSize: 2);

        Assert.False(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Fact]
    public void EmptyResultSet_HasNoNeighbours()
    {
        var page = new PagedList<int>(Array.Empty<int>(), count: 0, page: 1, pageSize: 10);

        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.False(page.HasPrevious);
        Assert.Empty(page.Data);
    }

    [Fact]
    public void CarriesTheRequestedPageMetadata()
    {
        var page = new PagedList<string>(new[] { "a" }, count: 42, page: 3, pageSize: 5);

        Assert.Equal(3, page.CurrentPage);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(42, page.TotalCount);
    }

    [Fact]
    public void ToPagedList_WrapsAnInMemorySequence()
    {
        var page = new[] { 1, 2, 3 }.ToPagedList(count: 30, pageNumber: 1, pageSize: 3);

        Assert.Equal(10, page.TotalPages);
        Assert.Equal(new[] { 1, 2, 3 }, page.Data);
    }
}
