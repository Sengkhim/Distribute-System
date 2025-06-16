namespace DS.InventoryService.Domain.Entities;

public class OrderItems
{
    public required string Id { get; set; }
    public required string OrderId { get; set; }
    public required string ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal { get; set; }
    public string ProductSnapshot { get; set; } = string.Empty;
    public Orders? Orders { get; set; }
}