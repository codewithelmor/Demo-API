


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.Orders;

// EP-38
public sealed class CreateOrderRequest
{
    public Guid CustomerId { get; set; }

    public List<OrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateOrderValidator : Validator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(r => r.CustomerId).NotEmpty();
        RuleFor(r => r.Items).Items();
        RuleForEach(r => r.Items).SetValidator(new OrderItemRequestValidator());
    }
}

public sealed class CreateOrderEndpoint : Endpoint<CreateOrderRequest, OrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Create an order for a customer";
            s.Description = "Creates a Pending order at current prices and reserves stock. 400 for an unknown customer or product, 409 insufficient-stock or too-many-pending-orders. Returns 201 with Location and ETag.";
        });
    }

    public override async Task HandleAsync(CreateOrderRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new CreateOrderCommand(r.CustomerId, OrderItemRequest.ToInput(r.Items)).ExecuteAsync(ct))
            .ToCreated(HttpContext, o => $"/api/v1/orders/{o.Id}"));
}

// EP-39
public sealed class ListOrdersRequest : OrderFilterRequest;

public sealed class ListOrdersValidator : OrderFilterValidator<ListOrdersRequest>;

public sealed class ListOrdersEndpoint(IQueryHandler<ListOrdersQuery, PagedResponse<OrderListItem>> handler) : Endpoint<ListOrdersRequest, PagedResponse<OrderListItem>>
{
    public override void Configure()
    {
        Get("/orders");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "List orders";
            s.Description = "Paged order list, newest first by default. Filters: status (repeatable), customerId, createdFrom (inclusive), createdTo (exclusive), minTotal, maxTotal.";
        });
    }

    public override async Task HandleAsync(ListOrdersRequest r, CancellationToken ct) =>
        await Send.OkAsync(await handler.HandleAsync(new ListOrdersQuery(r.ToPage(), r.ToSort(), r.ToFilter()), ct), ct);
}

// EP-40, EP-47
public sealed class OrderRouteRequest
{
    public Guid OrderId { get; set; }
}

public sealed class GetOrderByIdEndpoint(IQueryHandler<GetOrderQuery, Result<Tagged<OrderResponse>>> handler) : Endpoint<OrderRouteRequest, OrderResponse>
{
    public override void Configure()
    {
        Get("/orders/{orderId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Get an order";
            s.Description = "Returns one order with items, status history and the allowed next statuses, plus its ETag.";
        });
    }

    public override async Task HandleAsync(OrderRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new GetOrderQuery(r.OrderId), ct)).ToOk(HttpContext));
}

// EP-41
public sealed class ReplaceOrderItemsRequest
{
    public Guid OrderId { get; set; }

    [FromHeader("If-Match", false)]
    public string? IfMatch { get; set; }

    public List<OrderItemRequest> Items { get; set; } = [];
}

public sealed class ReplaceOrderItemsValidator : Validator<ReplaceOrderItemsRequest>
{
    public ReplaceOrderItemsValidator()
    {
        RuleFor(r => r.Items).Items();
        RuleForEach(r => r.Items).SetValidator(new OrderItemRequestValidator());
    }
}

public sealed class ReplaceOrderItemsEndpoint : Endpoint<ReplaceOrderItemsRequest, OrderResponse>
{
    public override void Configure()
    {
        Put("/orders/{orderId}/items");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Replace the items of an order";
            s.Description = "Replaces all lines of a Pending order and adjusts stock. Requires If-Match: 428 when missing, 412 when stale. 409 invalid-order-state or insufficient-stock.";
        });
    }

    public override async Task HandleAsync(ReplaceOrderItemsRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ReplaceOrderItemsCommand(r.OrderId, r.IfMatch, OrderItemRequest.ToInput(r.Items)).ExecuteAsync(ct)).ToOk(HttpContext));
}

// EP-42
public sealed class ChangeOrderStatusRequest
{
    public Guid OrderId { get; set; }

    public OrderStatus? Status { get; set; }

    public string? Reason { get; set; }
}

public sealed class ChangeOrderStatusValidator : Validator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusValidator()
    {
        RuleFor(r => r.Status).NotNull().IsInEnum();
        RuleFor(r => r.Reason).Reason();
    }
}

public sealed class ChangeOrderStatusEndpoint : Endpoint<ChangeOrderStatusRequest>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/status");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Change the status of an order";
            s.Description = "Moves an order forward: Pending → Confirmed → Shipped → Completed. Other targets return 409 invalid-status-transition with allowedNextStatuses; use the cancel endpoint to cancel.";
        });
    }

    public override async Task HandleAsync(ChangeOrderStatusRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ChangeOrderStatusCommand(r.OrderId, r.Status!.Value, r.Reason).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-43, EP-48
public sealed class CancelOrderRequest
{
    public Guid OrderId { get; set; }

    public string? Reason { get; set; }
}

public sealed class CancelOrderValidator : Validator<CancelOrderRequest>
{
    public CancelOrderValidator() => RuleFor(r => r.Reason).Reason();
}

public sealed class CancelOrderEndpoint : Endpoint<CancelOrderRequest>
{
    public override void Configure()
    {
        Post("/orders/{orderId}/cancel");
        Policies(AuthPolicies.AdminOnly);
        Description(x => x.ClearDefaultAccepts().Accepts<CancelOrderRequest>(isOptional: true, "application/json", "*/*"));
        Summary(s =>
        {
            s.Summary = "Cancel an order";
            s.Description = "Cancels a Pending or Confirmed order and returns its stock. The body is optional ({ reason }). 409 invalid-status-transition otherwise.";
        });
    }

    public override async Task HandleAsync(CancelOrderRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new CancelOrderCommand(r.OrderId, r.Reason).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-45
public sealed class PlaceMyOrderRequest
{
    public List<OrderItemRequest> Items { get; set; } = [];
}

public sealed class PlaceMyOrderValidator : Validator<PlaceMyOrderRequest>
{
    public PlaceMyOrderValidator()
    {
        RuleFor(r => r.Items).Items();
        RuleForEach(r => r.Items).SetValidator(new OrderItemRequestValidator());
    }
}

public sealed class PlaceMyOrderEndpoint : Endpoint<PlaceMyOrderRequest, MyOrderResponse>
{
    public override void Configure()
    {
        Post("/customers/me/orders");
        Policies(AuthPolicies.CustomerOnly);
        Summary(s =>
        {
            s.Summary = "Place an order";
            s.Description = "Places a Pending order for the caller (customer ID from the token only). 400 for an unknown product, 409 insufficient-stock or too-many-pending-orders. Returns 201 with Location and ETag.";
        });
    }

    public override async Task HandleAsync(PlaceMyOrderRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await new PlaceMyOrderCommand(customerId, OrderItemRequest.ToInput(r.Items)).ExecuteAsync(ct))
                .ToCreated(HttpContext, o => $"/api/v1/customers/me/orders/{o.Id}"));
    }
}

// EP-46
public sealed class ListMyOrdersRequest : PagedRequest
{
    public List<OrderStatus>? Status { get; set; }

    public string? CreatedFrom { get; set; }

    public string? CreatedTo { get; set; }
}

public sealed class ListMyOrdersValidator : PagedValidator<ListMyOrdersRequest>
{
    public ListMyOrdersValidator()
        : base(OrderSortFields.Customer, OrderSortFields.Default)
    {
        RuleForEach(r => r.Status).IsInEnum();
        RuleFor(r => r.CreatedFrom).IsoDate();
        RuleFor(r => r.CreatedTo).IsoDate()
            .Must((r, to) => Rules.FromBeforeTo(r.CreatedFrom, to)).WithMessage("createdTo must be after createdFrom.");
    }
}

public sealed class ListMyOrdersEndpoint(IQueryHandler<ListMyOrdersQuery, PagedResponse<MyOrderListItem>> handler) : Endpoint<ListMyOrdersRequest, PagedResponse<MyOrderListItem>>
{
    public override void Configure()
    {
        Get("/customers/me/orders");
        Policies(AuthPolicies.CustomerOnly);
        Summary(s =>
        {
            s.Summary = "List my orders";
            s.Description = "Paged list of the caller's orders, newest first by default. Filters: status (repeatable), createdFrom (inclusive), createdTo (exclusive).";
        });
    }

    public override async Task HandleAsync(ListMyOrdersRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        if (customerId == Guid.Empty)
        {
            await Send.ResultAsync(Error.NotFound().ToProblem(HttpContext));
            return;
        }

        var query = new ListMyOrdersQuery(
            customerId, r.ToPage(), r.ToSort(OrderSortFields.Customer, OrderSortFields.Default), r.Status?.Distinct().ToList() ?? [],
            Rules.ParseIso(r.CreatedFrom), Rules.ParseIso(r.CreatedTo));
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}

// EP-47
public sealed class GetMyOrderEndpoint(IQueryHandler<GetMyOrderQuery, Result<Tagged<MyOrderResponse>>> handler) : Endpoint<OrderRouteRequest, MyOrderResponse>
{
    public override void Configure()
    {
        Get("/customers/me/orders/{orderId}");
        Policies(AuthPolicies.CustomerOnly);
        Summary(s =>
        {
            s.Summary = "Get my order";
            s.Description = "Returns one of the caller's orders with items and history, plus its ETag. Another customer's order returns 404.";
        });
    }

    public override async Task HandleAsync(OrderRouteRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await handler.HandleAsync(new GetMyOrderQuery(customerId, r.OrderId), ct)).ToOk(HttpContext));
    }
}

// EP-48
public sealed class CancelMyOrderEndpoint : Endpoint<CancelOrderRequest>
{
    public override void Configure()
    {
        Post("/customers/me/orders/{orderId}/cancel");
        Policies(AuthPolicies.CustomerOnly);
        Description(x => x.ClearDefaultAccepts().Accepts<CancelOrderRequest>(isOptional: true, "application/json", "*/*"));
        Summary(s =>
        {
            s.Summary = "Cancel my order";
            s.Description = "Cancels one of the caller's Pending orders and returns its stock. The body is optional ({ reason }). 404 for another customer's order; 409 invalid-status-transition otherwise.";
        });
    }

    public override async Task HandleAsync(CancelOrderRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await new CancelMyOrderCommand(customerId, r.OrderId, r.Reason).ExecuteAsync(ct)).ToNoContent(HttpContext));
    }
}