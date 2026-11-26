using Adhicom.Domain.Subscriptions;

namespace Adhicom.UnitTests;

public class CurrencyAndPlanTests
{
    [Fact]
    public void Plan_Entity_Defaults_Are_Free_Friendly()
    {
        var plan = new Plan { Key = "free", Name = "Gratuit" };
        Assert.Equal("free", plan.Key);
        Assert.Equal(1, plan.MaxStores);
        Assert.True(plan.MaxProducts > 0);
        Assert.False(plan.AllowAdvancedAnalytics);
    }

    [Theory]
    [InlineData(0, "USD")]
    [InlineData(1500000.5, "CDF")]
    [InlineData(999.99, "XAF")]
    public void Plan_MonthlyPrice_Accepts_Decimal_Amounts(decimal price, string currency)
    {
        var plan = new Plan { Key = "pro", Name = "Pro", MonthlyPrice = price, Currency = currency };
        Assert.Equal(price, plan.MonthlyPrice);
        Assert.Equal(currency, plan.Currency);
    }
}
