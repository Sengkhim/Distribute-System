namespace DS.Shared.Events;

// Commands (tell a service to do something)
public record OrderItemResponse(string ProductId, int Quantity, decimal UnitPrice);
public record PlaceOrderCommand(string OrderId, string UserId, IEnumerable<OrderItemResponse> Items);
public record ReserveInventoryCommand(string OrderId, string ProductId, int Quantity);
public record ReleaseInventoryCommand(string OrderId, string ProductId, int Quantity);
public record ProcessPaymentCommand(string OrderId, decimal Amount, string UserId);
public record ConfirmOrderCommand(string OrderId);
public record CancelOrderCommand(string OrderId);

// Events (a service has done something, or something has happened)
public record OrderCreatedEvent(string OrderId, string UserId, decimal TotalAmount, DateTimeOffset CreatedAt);
public record InventoryReservedEvent(string OrderId, string ProductId, int Quantity);
public record InventoryReservationFailedEvent(string OrderId, string ProductId, int RequestedQuantity, string Reason);
public record PaymentProcessedEvent(string OrderId, decimal Amount, string UserId, string TransactionId);
public record PaymentFailedEvent(string OrderId, decimal Amount, string UserId, string Reason);
public record OrderConfirmedEvent(string OrderId);
public record OrderCancelledEvent(string OrderId,  string Reason);