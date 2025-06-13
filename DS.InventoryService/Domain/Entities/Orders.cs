namespace DS.InventoryService.Domain.Entities;

public class Orders
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TransactionNo { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public ICollection<OrderItems> OrderItems { get; set; } = new List<OrderItems>();
}