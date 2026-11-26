namespace Rmg.Modules.Merchandising.Contracts;

// ORD customer-order endpoint payloads; success responses are Envelope-wrapped by the controller (R-1).
public sealed record OrderCreateRequest(string? Code, Guid BuyerId, string? BuyerPo, string? Currency, DateOnly OrderDate, OrderLineInput[]? Lines);

public sealed record OrderLineInput(Guid StyleVersionId, decimal Price, OrderSizeInput[]? Sizes, OrderScheduleInput[]? Schedules);

public sealed record OrderSizeInput(string? ColorCode, string? SizeCode, decimal Qty);

public sealed record OrderScheduleInput(DateOnly ShipDate, decimal Qty);

public sealed record OrderConfirmRequest(int ExpectedRevision);

public sealed record OrderCancelRequest(string? Reason);

// Read contract — lineQty is the server-derived Σ of the size quantities (never echoed from a client).
public sealed record OrderSizeDto(string ColorCode, string SizeCode, decimal Qty);

public sealed record OrderScheduleDto(DateOnly ShipDate, decimal Qty);

public sealed record OrderLineDto(Guid Id, int LineNo, Guid StyleId, Guid StyleVersionId, decimal Price, decimal LineQty, OrderSizeDto[] Sizes, OrderScheduleDto[] Schedules);

public sealed record OrderDto(Guid Id, string Code, Guid BuyerId, string BuyerPo, string Currency, DateOnly OrderDate, string Status, int Revision, OrderLineDto[] Lines);
