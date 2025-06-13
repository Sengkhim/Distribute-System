using DS.Shared.Events;

namespace DS.Orchestrator;

using NServiceBus;
using NServiceBus.Logging;
using System.Threading.Tasks;

// 1. Define Saga Data (persisted state for the saga instance)
public class OrderSagaData : ContainSagaData
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool OrderConfirmed { get; set; } // Final state
    public string CurrentState { get; set; } = string.Empty; // Useful for tracking saga progress (e.g., "PendingInventory", "PendingPayment") 
    public string ReasonForFailure { get; set; } = string.Empty;
}

// 2. Define the Saga itself
public class OrderSaga :
    Saga<OrderSagaData>,
    IAmStartedByMessages<PlaceOrderCommand>, // Saga starts when PlaceOrderCommand is received
    IHandleMessages<InventoryReservedEvent>,
    IHandleMessages<InventoryReservationFailedEvent>,
    IHandleMessages<PaymentProcessedEvent>,
    IHandleMessages<PaymentFailedEvent>,
    IHandleMessages<OrderConfirmedEvent>,
    IHandleMessages<OrderCancelledEvent>
{
    private static readonly ILog Log = LogManager.GetLogger<OrderSaga>();

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.OrderId)
            .ToMessage<PlaceOrderCommand>(message => message.OrderId)
            .ToMessage<InventoryReservedEvent>(message => message.OrderId)
            .ToMessage<InventoryReservationFailedEvent>(message => message.OrderId)
            .ToMessage<PaymentProcessedEvent>(message => message.OrderId)
            .ToMessage<PaymentFailedEvent>(message => message.OrderId)
            .ToMessage<OrderConfirmedEvent>(message => message.OrderId)
            .ToMessage<OrderCancelledEvent>(message => message.OrderId);
    }

    // --- Handlers for incoming messages ---

    // Saga Start: PlaceOrderCommand from Client or API Gateway
    public async Task Handle(PlaceOrderCommand message, IMessageHandlerContext context)
    {
        Log.Info($"Saga for Order {message.OrderId} started.");
        Data.OrderId = message.OrderId;
        Data.CustomerId = message.UserId;
        Data.TotalAmount = message.Items.Sum(i => i.Quantity * i.UnitPrice); // Assuming total amount is set here or passed
        Data.CurrentState = "PendingInventory";

        // Send commands to other services
        foreach (var item in message.Items)
        {
            await context.Send(new ReserveInventoryCommand(message.OrderId, item.ProductId, item.Quantity));
        }
        
        var sendOptions = new SendOptions();
        sendOptions.DelayDeliveryWith(TimeSpan.FromSeconds(30));
        
        // Set a timeout in case inventory reservation takes too long
        await context.Send(new OrderTimeoutCommand(message.OrderId), sendOptions);
    }

    // Handle Inventory Reserved
    public async Task Handle(InventoryReservedEvent message, IMessageHandlerContext context)
    {
        Log.Info($"Saga for Order {Data.OrderId}: Inventory for product {message.ProductId} reserved.");
        Data.InventoryReserved = true; // For simplicity, assume all inventory is reserved in one go. In reality, you'd track per-product.
        Data.CurrentState = "PendingPayment";

        // Now, proceed to payment
        await context.Send(new ProcessPaymentCommand(Data.OrderId, Data.TotalAmount, Data.CustomerId));
    }

    // Handle Inventory Reservation Failed (Compensating transaction)
    public async Task Handle(InventoryReservationFailedEvent message, IMessageHandlerContext context)
    {
        Log.Warn($"Saga for Order {Data.OrderId}: Inventory reservation failed for product {message.ProductId}. Reason: {message.Reason}. Cancelling order.");
        Data.ReasonForFailure = message.Reason;
        Data.CurrentState = "InventoryReservationFailed";

        // Send a command to cancel the order in the Inventory/Sale Service
        await context.Send(new CancelOrderCommand(Data.OrderId));
        MarkAsComplete(); // End the saga as it failed
    }

    // Handle Payment Processed
    public async Task Handle(PaymentProcessedEvent message, IMessageHandlerContext context)
    {
        Log.Info($"Saga for Order {Data.OrderId}: Payment processed. Transaction ID: {message.TransactionId}");
        Data.PaymentProcessed = true;
        Data.CurrentState = "PendingConfirmation";

        // Finalize the order in the Inventory/Sale Service
        await context.Send(new ConfirmOrderCommand(Data.OrderId));
    }

    // Handle Payment Failed (Compensating transaction)
    public async Task Handle(PaymentFailedEvent message, IMessageHandlerContext context)
    {
        Log.Warn($"Saga for Order {Data.OrderId}: Payment failed. Reason: {message.Reason}. Releasing inventory and cancelling order.");
        Data.ReasonForFailure = message.Reason;
        Data.CurrentState = "PaymentFailed";

        // Compensating transactions:
        // 1. Release inventory (send command to Inventory/Sale service)
        await context.Send(new ReleaseInventoryCommand(Data.OrderId, Guid.Empty.ToString(), 0)); // Simplified: Release all for this order. Real: track individual product reservations.
        // 2. Cancel the order (send command to Inventory/Sale service)
        await context.Send(new CancelOrderCommand(Data.OrderId));

        MarkAsComplete(); // End the saga as it failed
    }

    // Handle Order Confirmed (Success path)
    public Task Handle(OrderConfirmedEvent message, IMessageHandlerContext context)
    {
        Log.Info($"Saga for Order {Data.OrderId}: Order confirmed successfully!");
        Data.OrderConfirmed = true;
        Data.CurrentState = "Completed";
        MarkAsComplete(); // End the saga successfully
        return Task.CompletedTask;
    }

    // Handle Order Cancelled (Final state after failure)
    public Task Handle(OrderCancelledEvent message, IMessageHandlerContext context)
    {
        Log.Info($"Saga for Order {Data.OrderId}: Order has been cancelled. Reason: {message.Reason}");
        Data.CurrentState = "Cancelled";
        MarkAsComplete(); // End the saga after cancellation
        return Task.CompletedTask;
    }

    // Example Timeout Handler (for the SendWithDelay above)
    public async Task Handle(OrderTimeoutCommand message, IMessageHandlerContext context)
    {
        // Check if the order is still pending. If so, something went wrong, and we need to cancel.
        if (Data.CurrentState != "Completed" && Data.CurrentState != "Cancelled")
        {
            Log.Error($"Saga for Order {Data.OrderId} timed out. Cancelling order.");
            Data.ReasonForFailure = "Timeout";
            Data.CurrentState = "TimedOut";
            await context.Send(new ReleaseInventoryCommand(Data.OrderId, Guid.Empty.ToString(), 0)); // Release inventory
            await context.Send(new CancelOrderCommand(Data.OrderId)); // Cancel order
            MarkAsComplete();
        }
    }
}

// Custom command for saga timeout (must be a message type)
public class OrderTimeoutCommand(string orderId) : ICommand
{
    public string OrderId { get; set; } = orderId;
}