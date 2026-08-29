using Jaberah.Models.JaberahModels;
using Jaberah.Queries;
using Xunit;

namespace Jaberah.Tests;

public class StudentQueriesTests
{
    private static IQueryable<Student> Sample() => new List<Student>
    {
        new() { Id = 1, StudentName = "أحمد",  MemoRate = 10, GroupId = 1 },
        new() { Id = 2, StudentName = "محمد",  MemoRate = 50, GroupId = 1 },
        new() { Id = 3, StudentName = "خالد",  MemoRate = 90, GroupId = null },
        new() { Id = 4, StudentName = "محمود", MemoRate = 30, GroupId = 2 },
        new() { Id = 5, StudentName = "سعيد",  MemoRate = 70, GroupId = null },
    }.AsQueryable();

    [Fact]
    public void FilterAndSort_OrdersByMemoRateDescending()
    {
        var result = StudentQueries.FilterAndSort(Sample()).ToList();

        Assert.Equal(new[] { 3, 5, 2, 4, 1 }, result.Select(s => s.Id));
    }

    /// <summary>
    /// Regression test. The list endpoint used to call OrderByDescending AFTER
    /// Skip/Take, which sorted only the rows already on the page. With 5 students
    /// the top scorer (id 3, rate 90) never appeared on page 1 of size 2.
    /// </summary>
    [Fact]
    public void FilterAndSort_SortsBeforePaging_SoPageOneHoldsTheTopScorers()
    {
        const int pageNumber = 1;
        const int pageSize = 2;

        var page = StudentQueries.FilterAndSort(Sample())
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        Assert.Equal(new[] { 3, 5 }, page.Select(s => s.Id));
        Assert.Equal(90, page[0].MemoRate);
    }

    [Fact]
    public void FilterAndSort_KeepsOrderingAcrossPages()
    {
        const int pageSize = 2;
        var sorted = StudentQueries.FilterAndSort(Sample());

        var pageTwo = sorted.Skip(pageSize).Take(pageSize).Select(s => s.Id).ToList();

        Assert.Equal(new[] { 2, 4 }, pageTwo);
    }

    [Fact]
    public void FilterAndSort_EmptySearchTextReturnsEveryStudent()
    {
        var result = StudentQueries.FilterAndSort(Sample(), searchText: "");

        Assert.Equal(5, result.Count());
    }

    [Fact]
    public void FilterAndSort_MatchesOnPartialName()
    {
        var result = StudentQueries.FilterAndSort(Sample(), searchText: "محم").ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Contains("محم", s.StudentName));
    }

    [Fact]
    public void FilterAndSort_WithoutGroup_ReturnsOnlyUnassignedStudents()
    {
        var result = StudentQueries.FilterAndSort(Sample(), withoutGroup: true).ToList();

        Assert.Equal(new[] { 3, 5 }, result.Select(s => s.Id));
        Assert.All(result, s => Assert.Null(s.GroupId));
    }

    [Fact]
    public void FilterAndSort_CombinesSearchAndWithoutGroup()
    {
        var result = StudentQueries.FilterAndSort(Sample(), "سعيد", withoutGroup: true).ToList();

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void FilterAndSort_NoMatchesReturnsEmpty()
    {
        Assert.Empty(StudentQueries.FilterAndSort(Sample(), "لا يوجد"));
    }
}
