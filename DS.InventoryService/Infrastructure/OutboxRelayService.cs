using Confluent.Kafka;
using DS.InventoryService.Infrastructure.EFCoreDbContext;
using Microsoft.EntityFrameworkCore;

namespace DS.InventoryService.Infrastructure;

public class OutboxRelayService(
    IServiceProvider serviceProvider,
    ILogger<OutboxRelayService> logger,
    IProducer<string, string> kafkaProducer)
    : BackgroundService
{
    private const string OutboxTopic = "outbox-events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Relay Service running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

                // Select unprocessed messages, order by CreatedAt to process in order
                var messages = await dbContext.OutboxMessages
                    .Where(m => m.Processed == false)
                    .OrderBy(m => m.CreatedAt)
                    .Take(100) // Process in batches
                    .ToListAsync(stoppingToken);

                if (messages.Count != 0)
                {
                    logger.LogInformation("Found {Count} outbox messages to process.", messages.Count);

                    foreach (var message in messages)
                    {
                        try
                        {
                            // Publish to Kafka
                            await kafkaProducer.ProduceAsync(
                                OutboxTopic,
                                new Message<string, string> { Key = message.AggregateId, Value = message.Payload },
                                stoppingToken);

                            // Mark as processed
                            message.ProcessedAt = DateTime.UtcNow;
                            message.Processed = true;
                            logger.LogInformation("Published outbox message {MessageId} (Type: {Type}) to Kafka.", message.Id, message.Type);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to publish outbox message {MessageId} to Kafka.", message.Id);
                            // Important: Do NOT mark as processed if publishing fails.
                            // The message will be retried in the next polling cycle.
                        }
                    }
                    await dbContext.SaveChangesAsync(stoppingToken); // Save processed status
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Poll every 5 seconds
        }
    }
}