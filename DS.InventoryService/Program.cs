using Confluent.Kafka;
using DS.InventoryService.Infrastructure;
using DS.InventoryService.Application.Handlers;
using DS.InventoryService.Infrastructure.EFCoreDbContext;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IN_Connection")));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig { BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<OutboxRelayService>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PlaceOrderCommandHandler).Assembly));


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();