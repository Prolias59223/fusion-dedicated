using FusionDedicated;
using FusionDedicated.Server;

namespace FusionDedicated.Tests;

public class RelayBudgetTests
{
    [Fact]
    public void Traffic_under_the_budget_passes()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 100, windowSeconds: 60);

        for (int i = 0; i < 50; i++)
        {
            Assert.True(budget.Allow(1, 1024));
        }
    }

    [Fact]
    public void Over_the_budget_is_dropped_and_reports_the_overrun()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 10, windowSeconds: 60);

        Assert.True(budget.Allow(1, 8 * 1024));
        Assert.True(budget.Allow(1, 1 * 1024));

        Assert.False(budget.Allow(1, 2 * 1024));
        Assert.NotNull(budget.LastOverrun);
        Assert.Contains("relay budget", budget.LastOverrun);
    }

    [Fact]
    public void The_window_frees_capacity_again()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 1, windowSeconds: 1);

        Assert.True(budget.Allow(1, 1024));
        Assert.False(budget.Allow(1, 1024));

        Thread.Sleep(1100);

        Assert.True(budget.Allow(1, 1024));
    }

    [Fact]
    public void Players_are_budgeted_separately()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 1, windowSeconds: 60);

        Assert.True(budget.Allow(1, 1024));
        Assert.False(budget.Allow(1, 1024), "player 1 has spent theirs");
        Assert.True(budget.Allow(2, 1024), "player 2's budget is their own");
    }

    [Fact]
    public void Forget_clears_a_players_spend()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 1, windowSeconds: 60);

        Assert.True(budget.Allow(1, 1024));
        Assert.False(budget.Allow(1, 1024));

        budget.Forget(1);

        Assert.True(budget.Allow(1, 1024));
    }

    [Fact]
    public void Zero_disables_the_budget()
    {
        var budget = new RelayBudget(kilobytesPerSecond: 0);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(budget.Allow(1, 64 * 1024));
        }

        Assert.Null(budget.LastOverrun);
    }

    [Fact]
    public void The_window_is_never_shorter_than_a_second()
    {
        // A misconfigured zero would make the window a divide-by-zero or an
        // always-expired queue, depending on the maths — clamp instead.
        var budget = new RelayBudget(kilobytesPerSecond: 1, windowSeconds: 0);

        Assert.True(budget.Allow(1, 1024));
        Assert.False(budget.Allow(1, 1024));
    }
}
