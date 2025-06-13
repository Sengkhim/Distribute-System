namespace DS.InventoryService.Domain.Entities;

public class OutboxMessage
{
    public required string Id { get; set; }
    public string AggregateType { get; set; } = string.Empty; // "Order"
    public string AggregateId { get; set; } = string.Empty; // OrderId.ToString()
    public string Type { get; set; } = string.Empty; // "OrderCreatedEvent", "InventoryReservedEvent"
    public string Payload { get; set; } = string.Empty; // JSON serialized event/command
    public bool Processed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}