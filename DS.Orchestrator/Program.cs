using NServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using DS.Shared.Events;
using NServiceBus.Transport.Kafka;

IHost host = Host.CreateDefaultBuilder(args)
    .UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("OrderSagaOrchestrator");

        // Use a transport (Kafka)
        var kafkaConnection = context.Configuration.GetValue<string>("Kafka:BootstrapServers");
        var transport = endpointConfiguration.UseTransport<KafkaTransport>();
        transport.ConnectionString(kafkaConnection);

        // Define routing for commands/events
        var routing = transport.Routing();
        // Commands for InventorySaleService
        routing.RouteToEndpoint(typeof(ReserveInventoryCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(ReleaseInventoryCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(ConfirmOrderCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(CancelOrderCommand), "InventorySaleService");
        // Commands for a hypothetical PaymentService
        routing.RouteToEndpoint(typeof(ProcessPaymentCommand), "PaymentService");

        // Events that this saga subscribes to (from other services)
        endpointConfiguration.SubscribeToEvent<InventoryReservedEvent>(EventSubscription.Custom("InventorySaleService"));
        endpointConfiguration.SubscribeToEvent<InventoryReservationFailedEvent>(EventSubscription.Custom("InventorySaleService"));
        endpointConfiguration.SubscribeToEvent<PaymentProcessedEvent>(EventSubscription.Custom("PaymentService"));
        endpointConfiguration.SubscribeToEvent<PaymentFailedEvent>(EventSubscription.Custom("PaymentService"));
        endpointConfiguration.SubscribeToEvent<OrderConfirmedEvent>(EventSubscription.Custom("InventorySaleService"));
        endpointConfiguration.SubscribeToEvent<OrderCancelledEvent>(EventSubscription.Custom("InventorySaleService"));

        // Configure Saga Persister (PostgreSQL for NServiceBus Saga Data)
        var connectionString = context.Configuration.GetConnectionString("NServiceBusSagaStore");
        var persistence = endpointConfiguration.UsePersistence<PostgreSqlPersistence>();
        persistence.ConnectionBuilder(
            connectionBuilder: () => new Npgsql.NpgsqlConnection(connectionString));
        persistence.CreateTableWith==NServiceBus.SqlServerTransport.TableCreationMode.DevelopmentOrTest); // For dev only!

        // Outbox/Inbox (NServiceBus handles this implicitly when using NServiceBus Outbox)
        endpointConfiguration.EnableOutbox();
        endpointConfiguration.EnableInstallers(); // Creates tables if not present (dev only)

        endpointConfiguration.Conventions()
            .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith("Commands"))
            .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith("Events"));

        // Choose a serializer
        endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();

        return endpointConfiguration;
    })
    .Build();

await host.RunAsync();