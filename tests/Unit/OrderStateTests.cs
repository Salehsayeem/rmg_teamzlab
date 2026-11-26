using Rmg.BuildingBlocks.Common;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.UnitTests;

// Task 14 (ORD): the customer-order state machine lives on the entity — the write-side guard.
// Confirm moves Draft→Confirmed only; cancel moves Draft/Confirmed→Cancelled only; moving outside
// the allowed set throws InvalidStateProblem and leaves the status untouched.
public class OrderStateTests
{
    [Fact]
    public void Order_Confirm_Twice_Throws()
    {
        var order = new CustomerOrder { Status = "Draft" };

        order.Confirm();

        Assert.Equal("Confirmed", order.Status);
        Assert.Throws<InvalidStateProblem>(() => order.Confirm()); // already Confirmed
    }

    [Fact]
    public void Order_Cancel_Then_Confirm_Throws()
    {
        var order = new CustomerOrder { Status = "Draft" };

        order.Cancel();
        Assert.Equal("Cancelled", order.Status);

        Assert.Throws<InvalidStateProblem>(() => order.Confirm()); // Cancelled is terminal for confirm
        Assert.Throws<InvalidStateProblem>(() => order.Cancel());  // already Cancelled
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Confirmed", false)] // INVALID_STATE
    [InlineData("Cancelled", false)] // INVALID_STATE
    public void Order_Confirm_State_Table(string status, bool allowed)
    {
        var order = new CustomerOrder { Status = status };

        if (allowed)
        {
            order.Confirm();
            Assert.Equal("Confirmed", order.Status);
        }
        else
        {
            Assert.ThrowsAny<RmgProblemException>(() => order.Confirm());
            Assert.Equal(status, order.Status); // nothing moved on the rejected write
        }
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Confirmed", true)]
    [InlineData("Cancelled", false)] // INVALID_STATE
    public void Order_Cancel_State_Table(string status, bool allowed)
    {
        var order = new CustomerOrder { Status = status };

        if (allowed)
        {
            order.Cancel();
            Assert.Equal("Cancelled", order.Status);
        }
        else
        {
            Assert.ThrowsAny<RmgProblemException>(() => order.Cancel());
            Assert.Equal(status, order.Status);
        }
    }
}
