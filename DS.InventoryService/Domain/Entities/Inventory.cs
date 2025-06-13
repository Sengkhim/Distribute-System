namespace DS.InventoryService.Domain.Entities;

public class Inventory
{
    public required string Id { get; set; }
    public required string ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public double Quantity { get; set; }
}