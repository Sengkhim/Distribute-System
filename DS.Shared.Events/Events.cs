namespace DS.Shared.Events;

// Commands (tell a service to do something)
public record OrderItemResponse(Guid ProductId, int Quantity, decimal UnitPrice);
public record PlaceOrderCommand(Guid OrderId, Guid UserId, IEnumerable<OrderItemResponse> Items);
public record ReserveInventoryCommand(Guid OrderId, Guid ProductId, int Quantity);
public record ReleaseInventoryCommand(Guid OrderId, Guid ProductId, int Quantity);
public record ProcessPaymentCommand(Guid OrderId, decimal Amount, Guid UserId);
public record ConfirmOrderCommand(Guid OrderId);
public record CancelOrderCommand(Guid OrderId);

// Events (a service has done something, or something has happened)
public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalAmount, DateTime CreatedAt);
public record InventoryReservedEvent(Guid OrderId, Guid ProductId, int Quantity);
public record InventoryReservationFailedEvent(Guid OrderId, Guid ProductId, int RequestedQuantity, string Reason);
public record PaymentProcessedEvent(Guid OrderId, decimal Amount, Guid UserId, string TransactionId);
public record PaymentFailedEvent(Guid OrderId, decimal Amount, Guid UserId, string Reason);
public record OrderConfirmedEvent(Guid OrderId);
public record OrderCancelledEvent(Guid OrderId,  string Reason);