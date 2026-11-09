using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Cli;

namespace FinancialAnalystsAPI.Tests.Api;

public class PlumbingTests
{
    private sealed record Item(int Id, string Name, int Score);

    private static readonly Item[] Items = [new(1, "b", 5), new(2, "a", 5), new(3, "c", 1)];

    private static readonly SortOptions<Item> Sorts = new SortOptions<Item>("name", defaultDescending: false, i => i.Id)
        .Add("name", i => i.Name)
        .Add("score", i => i.Score);

    private static int[] Sorted(string? sort, string? dir)
    {
        Assert.True(Sorts.TryApply(Items.AsQueryable(), new PagedQuery { Sort = sort, Dir = dir }, out var sorted));
        return sorted.Select(i => i.Id).ToArray();
    }

    [Fact]
    public void Sort_DefaultsAndDirections()
    {
        Assert.Equal([2, 1, 3], Sorted(null, null));
        Assert.Equal([3, 1, 2], Sorted("NAME", "desc"));     // keys are case-insensitive
        Assert.Equal([1, 2, 3], Sorted("score", "desc"));    // tie on 5 broken by Id
        Assert.Equal([3, 1, 2], Sorted("score", null));      // explicit key without dir: ascending
    }

    [Fact]
    public void Sort_RejectsUnknownKeys()
    {
        Assert.False(Sorts.TryApply(Items.AsQueryable(), new PagedQuery { Sort = "Id; DROP TABLE" }, out _));
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    public void PagedResult_TotalPages(int total, int pageSize, int expected) =>
        Assert.Equal(expected, new PagedResult<int>([], 1, pageSize, total).TotalPages);

    [Fact]
    public void Commands_SplitConfigurationOverridesFromCommandTokens()
    {
        Assert.True(Commands.TryParse(["apikey", "create", "ci", "--admin", "--ConnectionStrings:AnalystsDb=Data Source=x.db"],
            out var command, out var hostArgs));

        Assert.NotNull(command);
        Assert.Equal(["--ConnectionStrings:AnalystsDb=Data Source=x.db"], hostArgs);
    }

    [Fact]
    public void Commands_LeaveWebServerArgumentsAlone()
    {
        string[] args = ["--urls=http://localhost:5000"];

        Assert.False(Commands.TryParse(args, out var command, out var hostArgs));
        Assert.Null(command);
        Assert.Equal(args, hostArgs);
    }
}
