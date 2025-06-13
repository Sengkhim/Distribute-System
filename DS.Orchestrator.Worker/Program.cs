using NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using DS.Shared.Events;
using NServiceBus.Transport.Kafka; // Ensure this is present
using NServiceBus.Transport.PostgreSql;

IHost host = Host.CreateDefaultBuilder(args)
    .UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("OrderSagaOrchestrator");

        // Use Kafka Transport
        var kafkaConnection = context.Configuration.GetValue<string>("Kafka:BootstrapServers");
        // var transport = endpointConfiguration.UseTransport<KafkaTransport>(kafkaConnection);
        var transport = endpointConfiguration.UseTransport<KafkaTransport>();

        // --- Routing for Commands ---
        // Commands are direct messages to a specific endpoint.
        var routing = transport.Routing();
        // Commands for InventorySaleService
        routing.RouteToEndpoint(typeof(ReserveInventoryCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(ReleaseInventoryCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(ConfirmOrderCommand), "InventorySaleService");
        routing.RouteToEndpoint(typeof(CancelOrderCommand), "InventorySaleService");
        // Commands for a hypothetical PaymentService (if you had one)
        routing.RouteToEndpoint(typeof(ProcessPaymentCommand), "PaymentService");

        // --- Subscription to Events ---
        // For events, NServiceBus will automatically subscribe if a handler/saga exists.
        // You generally *don't* explicitly call "SubscribeToEvent" like a method.
        // Instead, you inform NServiceBus *who publishes* the event,
        // if the transport requires it (Kafka doesn't always strictly require this for pub/sub,
        // but it's good practice for clarity and specific routing scenarios).

        // For instance, if 'InventoryReservedEvent' is published by 'InventorySaleService':
        routing.RegisterPublisher(typeof(InventoryReservedEvent), "InventorySaleService");
        routing.RegisterPublisher(typeof(InventoryReservationFailedEvent), "InventorySaleService");
        routing.RegisterPublisher(typeof(OrderConfirmedEvent), "InventorySaleService");
        routing.RegisterPublisher(typeof(OrderCancelledEvent), "InventorySaleService");
        // If 'PaymentProcessedEvent' is published by 'PaymentService':
        routing.RegisterPublisher(typeof(PaymentProcessedEvent), "PaymentService");
        routing.RegisterPublisher(typeof(PaymentFailedEvent), "PaymentService");


        // Configure Saga Persister (PostgreSQL for NServiceBus Saga Data)
        var connectionString = context.Configuration.GetConnectionString("NServiceBusSagaStore");
        var persistence = endpointConfiguration.UsePersistence<PostgreSqlPersistence>();
        persistence.ConnectionBuilder(
            connectionBuilder: () => new Npgsql.NpgsqlConnection(connectionString));
        // IMPORTANT: For production, do NOT use DevelopmentOrTest.
        // Use migrations or a separate tool to manage schema.
        persistence.DatalockAndSchema(
            schema: "nservicebus", // You can define a custom schema for NSB tables
            tableCreationMode: NServiceBus.PostgreSql.TableCreationMode.DevelopmentOrTest);
        
        // Ensure NServiceBus knows about your saga data type
        persistence.Saga<OrderSagaData>().Use></OrderSagaData>
        // No explicit table creation if using migrations, just use the TableCreationMode for development
        // or ensure your schema includes the NSB tables.

        // Outbox/Inbox (NServiceBus handles this implicitly when using NServiceBus Outbox)
        endpointConfiguration.EnableOutbox();
        endpointConfiguration.EnableInstallers(); // Creates queues/tables for dev. Turn OFF for prod!

        // Conventions help NServiceBus identify your messages
        endpointConfiguration.Conventions()
            .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith("Commands"))
            .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith("Events"));

        // Choose a serializer
        endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();

        return endpointConfiguration;
    })
    .Build();

await host.RunAsync();