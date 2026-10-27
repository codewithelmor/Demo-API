namespace DemoApi.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 15, 0, TimeSpan.Zero);
    private static readonly Guid P1 = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid P2 = Guid.Parse("00000000-0000-7000-8000-000000000002");
    private static readonly Guid P3 = Guid.Parse("00000000-0000-7000-8000-000000000003");

    public static TheoryData<OrderStatus, OrderStatus> AllPairs()
    {
        var data = new TheoryData<OrderStatus, OrderStatus>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void Create_snapshots_prices_sums_total_and_starts_pending_with_history()
    {
        var order = NewOrder(new OrderLine(P1, 2, 10.50m), new OrderLine(P2, 1, 0.99m));

        order.Status.ShouldBe(OrderStatus.Pending);
        order.Total.ShouldBe(21.99m);
        order.Items.Count.ShouldBe(2);
        order.Items.Single(i => i.ProductId == P1).LineTotal.ShouldBe(21.00m);
        order.History.Single().FromStatus.ShouldBeNull();
        order.History.Single().ToStatus.ShouldBe(OrderStatus.Pending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Create_with_wrong_line_count_returns_validation_error(int count)
    {
        var lines = Enumerable.Range(0, count).Select(_ => new OrderLine(Guid.NewGuid(), 1, 1m)).ToList();

        var result = Order.Create(Guid.NewGuid(), "USD", lines);

        result.Error!.ValidationErrors!.Keys.ShouldContain("items");
    }

    [Fact]
    public void Create_with_fifty_lines_succeeds()
    {
        var lines = Enumerable.Range(0, 50).Select(_ => new OrderLine(Guid.NewGuid(), 1, 1m)).ToList();

        Order.Create(Guid.NewGuid(), "USD", lines).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_with_duplicate_product_returns_validation_error() =>
        Order.Create(Guid.NewGuid(), "USD", [new OrderLine(P1, 1, 1m), new OrderLine(P1, 2, 1m)]).IsFailure.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Create_with_quantity_out_of_range_names_the_line(int quantity) =>
        Order.Create(Guid.NewGuid(), "USD", [new OrderLine(P1, 1, 1m), new OrderLine(P2, quantity, 1m)])
            .Error!.ValidationErrors!.Keys.ShouldContain("items[1].quantity");

    [Fact]
    public void Create_with_total_above_maximum_is_rejected() =>
        Order.Create(Guid.NewGuid(), "USD", [new OrderLine(P1, 1000, 9_999_999_999.99m)]).Error!.Code.ShouldBe("order-total-too-large");

    [Theory]
    [MemberData(nameof(AllPairs))]
    [Story("US-205", "AC1")]
    public void ChangeStatus_allows_only_forward_steps(OrderStatus from, OrderStatus to)
    {
        var order = OrderIn(from);
        var allowed = (from, to) is (OrderStatus.Pending, OrderStatus.Confirmed)
            or (OrderStatus.Confirmed, OrderStatus.Shipped)
            or (OrderStatus.Shipped, OrderStatus.Completed);

        var result = order.ChangeStatus(to, "reason", Now);

        result.IsSuccess.ShouldBe(allowed);
        order.Status.ShouldBe(allowed ? to : from);
        if (!allowed)
        {
            result.Error!.Code.ShouldBe("invalid-status-transition");
            result.Error.Extensions!["currentStatus"].ShouldBe(from.ToString());
        }
    }

    [Fact]
    public void ChangeStatus_to_cancelled_points_to_the_cancel_endpoint()
    {
        var order = OrderIn(OrderStatus.Pending);

        var result = order.ChangeStatus(OrderStatus.Cancelled, null, Now);

        result.Error!.Detail.ShouldContain("cancel endpoint");
        ((string[])result.Error.Extensions!["allowedNextStatuses"]!).ShouldBe(["Confirmed"]);
    }

    [Fact]
    public void ChangeStatus_adds_history_and_touches_the_order()
    {
        var order = OrderIn(OrderStatus.Pending);

        order.ChangeStatus(OrderStatus.Confirmed, "  paid  ", Now).IsSuccess.ShouldBeTrue();

        order.LastModifiedAt.ShouldBe(Now);
        var last = order.History.Last();
        last.FromStatus.ShouldBe(OrderStatus.Pending);
        last.ToStatus.ShouldBe(OrderStatus.Confirmed);
        last.Reason.ShouldBe("paid");
    }

    [Theory]
    [InlineData(OrderStatus.Pending, false, true)]
    [InlineData(OrderStatus.Confirmed, false, true)]
    [InlineData(OrderStatus.Shipped, false, false)]
    [InlineData(OrderStatus.Completed, false, false)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.Pending, true, true)]
    [InlineData(OrderStatus.Confirmed, true, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    public void Cancel_follows_admin_and_customer_rules(OrderStatus from, bool byCustomer, bool allowed)
    {
        var order = OrderIn(from);

        var result = order.Cancel(byCustomer, null, Now);

        result.IsSuccess.ShouldBe(allowed);
        order.Status.ShouldBe(allowed ? OrderStatus.Cancelled : from);
    }

    [Fact]
    public void ReplaceItems_returns_stock_delta_per_product_and_recalculates_total()
    {
        var order = NewOrder(new OrderLine(P1, 2, 10m), new OrderLine(P2, 3, 1m));

        var result = order.ReplaceItems([new OrderLine(P1, 5, 99m), new OrderLine(P3, 1, 4m)], Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value[P1].ShouldBe(3);
        result.Value[P2].ShouldBe(-3);
        result.Value[P3].ShouldBe(1);
        order.ActiveItems.Select(i => i.ProductId).ShouldBe([P1, P3], ignoreOrder: true);
        order.ActiveItems.Single(i => i.ProductId == P1).UnitPrice.ShouldBe(10m, "the snapshot price of a kept line does not change");
        order.Total.ShouldBe(54m);
        order.Items.Single(i => i.ProductId == P2).IsDeleted.ShouldBeTrue("removed lines are soft deleted");
        order.LastModifiedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public void ReplaceItems_outside_pending_returns_invalid_order_state(OrderStatus status)
    {
        var order = OrderIn(status);

        var result = order.ReplaceItems([new OrderLine(P1, 1, 1m)], Now);

        result.Error!.Code.ShouldBe("invalid-order-state");
        result.Error.Extensions!["currentStatus"].ShouldBe(status.ToString());
    }

    [Fact]
    public void ReplaceItems_with_no_lines_returns_validation_error() =>
        NewOrder(new OrderLine(P1, 1, 1m)).ReplaceItems([], Now).Error!.Code.ShouldBe("validation-failed");

    [Theory]
    [InlineData(OrderStatus.Pending, "Confirmed")]
    [InlineData(OrderStatus.Confirmed, "Shipped")]
    [InlineData(OrderStatus.Shipped, "Completed")]
    [InlineData(OrderStatus.Completed, "")]
    [InlineData(OrderStatus.Cancelled, "")]
    public void NextStatuses_matches_the_status_machine(OrderStatus status, string expected) =>
        string.Join(',', Order.NextStatuses(status)).ShouldBe(expected);

    [Fact]
    public void InsufficientStock_and_pending_cap_errors_carry_extensions()
    {
        OrderErrors.InsufficientStock([P1]).Extensions!["productIds"].ShouldBe(new[] { P1 });
        OrderErrors.TooManyPendingOrders(5).Extensions!["maxPending"].ShouldBe(5);
    }

    private static Order NewOrder(params OrderLine[] lines) => Order.Create(Guid.NewGuid(), "USD", lines).Value;

    private static Order OrderIn(OrderStatus status)
    {
        var order = NewOrder(new OrderLine(P1, 1, 1m));
        if (status == OrderStatus.Cancelled)
        {
            order.Cancel(false, null, Now).IsSuccess.ShouldBeTrue();
        }
        else
        {
            foreach (var next in new[] { OrderStatus.Confirmed, OrderStatus.Shipped, OrderStatus.Completed })
            {
                if (order.Status == status)
                {
                    break;
                }

                order.ChangeStatus(next, null, Now).IsSuccess.ShouldBeTrue();
            }
        }

        order.Status.ShouldBe(status);
        return order;
    }
}