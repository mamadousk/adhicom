using Adhicom.Domain.Common;

namespace Adhicom.UnitTests;

public class OrderStatusMachineTests
{
    [Fact]
    public void Pending_CanGoTo_Confirmed_Cancelled_Refused()
    {
        var allowed = OrderStatusMachine.Allowed(OrderStatus.Pending);
        Assert.Equal(new[] { OrderStatus.Confirmed, OrderStatus.Cancelled, OrderStatus.Refused }, allowed);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Pending, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]
    [InlineData(OrderStatus.Refused, OrderStatus.Confirmed)]
    public void Forbidden_Transitions_Are_Rejected(OrderStatus from, OrderStatus to)
        => Assert.False(OrderStatusMachine.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.ReadyToShip)]
    [InlineData(OrderStatus.ReadyToShip, OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Returned)]
    public void Nominal_Flow_Is_Allowed(OrderStatus from, OrderStatus to)
        => Assert.True(OrderStatusMachine.CanTransition(from, to));

    [Fact]
    public void Terminal_Statuses_Have_No_Outgoing_Transitions()
    {
        Assert.True(OrderStatusMachine.IsTerminal(OrderStatus.Cancelled));
        Assert.True(OrderStatusMachine.IsTerminal(OrderStatus.Refused));
        Assert.True(OrderStatusMachine.IsTerminal(OrderStatus.Returned));
        Assert.False(OrderStatusMachine.IsTerminal(OrderStatus.Pending));
    }

    [Fact]
    public void Every_Enum_Value_Has_A_Defined_Row()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
            _ = OrderStatusMachine.Allowed(status); // ne doit jamais lever ni null
    }
}
