using System.Text.Json;
using DS.InventoryService.Domain.Entities;
using DS.InventoryService.Infrastructure.EFCoreDbContext;
using DS.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.InventoryService.Application.Handlers;

public abstract record PlaceOrderCommandDto(string CustomerId, List<OrderItemResponse> Items) : IRequest<string>;

public class PlaceOrderCommandHandler(InventoryDbContext dbContext) : IRequestHandler<PlaceOrderCommandDto, string>
{
    public async Task<string> Handle(PlaceOrderCommandDto request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = new Orders
            {
                Id = Guid.NewGuid().ToString(),
                Code = $"ORDER-{Guid.NewGuid().ToString().Take(5)}",
                OrderDate= DateTimeOffset.UtcNow,
                Status = "Pending",
                TransactionNo = $"TSN-{Guid.NewGuid().ToString().Take(5)}",
                CustomerId = request.CustomerId,
            };
            
            await dbContext.Orders.AddAsync(order, cancellationToken);

            decimal totalAmount = 0;
            var reservedProducts = new List<(string ProductId, int Quantity)>();

            foreach (var itemDto in request.Items)
            {
                var inventory = await dbContext.Inventory
                    .FirstOrDefaultAsync(i => i.ProductId == itemDto.ProductId, cancellationToken);

                if (inventory is null || inventory.Quantity < itemDto.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for product {itemDto.ProductId}");
                }
                
                var productSnapshot = new { Name = Guid.NewGuid().ToString().Take(5), Price = itemDto.UnitPrice }; 

                var orderItem = new OrderItems
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    Price = itemDto.UnitPrice,
                    Subtotal = itemDto.Quantity * itemDto.UnitPrice,
                    ProductSnapshot = JsonSerializer.Serialize(productSnapshot) 
                };
                dbContext.OrderItems.Add(orderItem);
                totalAmount += orderItem.Subtotal;
                
                inventory.Quantity -= itemDto.Quantity;
                reservedProducts.Add((itemDto.ProductId, itemDto.Quantity));
            }

            order.Total = totalAmount;
            order.Subtotal = totalAmount;

            // Add the event to the Outbox table
            var orderCreatedEvent = new OrderCreatedEvent(order.Id, Guid.NewGuid().ToString(), order.Total, order.OrderDate);
            var reserveInventoryCommand = new ReserveInventoryCommand(order.Id, reservedProducts.First().ProductId,
                reservedProducts.First().Quantity); // Simplified for single product for brevity

            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                AggregateType = "Order",
                AggregateId = order.Id,
                Type = nameof(OrderCreatedEvent),
                Payload = JsonSerializer.Serialize(orderCreatedEvent)
            });
            
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                AggregateType = "Order", // or "Inventory"
                AggregateId = order.Id,
                Type = nameof(ReserveInventoryCommand), // This command will be picked up by the Saga
                Payload = JsonSerializer.Serialize(reserveInventoryCommand)
            });


            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return order.Id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}