using System.Net.Mime;
using System.Reflection;

using FunctionSaga;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using Shopping.Contracts;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        services
            .AddScoped<SagaFunctions>()
            .AddMassTransit(configure =>
            {
                configure.AddConsumer<CartItemAddedConsumer>();

                configure.AddSagaStateMachine<ShoppingCartStateMachine, ShoppingCart>()
                        .InMemoryRepository();

                configure.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureJsonSerializerOptions(options =>
                    {
                        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

                        return options;
                    });

                    cfg.DefaultContentType = new ContentType(MediaTypeNames.Application.Json);
                    cfg.UseRawJsonDeserializer();

                    cfg.ReceiveEndpoint("saga-queue", endpointConfigurator =>
                    {
                        const int ConcurrencyLimit = 20; // this can go up, depending upon the database capacity

                        endpointConfigurator.PrefetchCount = ConcurrencyLimit;

                        endpointConfigurator.UseMessageRetry(r => r.Interval(5, 1000));
                        endpointConfigurator.UseInMemoryOutbox();

                        endpointConfigurator.ConfigureSaga<ShoppingCart>(context, s =>
                        {
                            var partition = endpointConfigurator.CreatePartitioner(ConcurrencyLimit);

                            s.Message<CartItemAdded>(x => x.UsePartitioner(partition, m => m.Message.UserName));
                            s.Message<OrderSubmitted>(x => x.UsePartitioner(partition, m => m.Message.CartId));
                        });
                    });

                    cfg.ConfigureEndpoints(context);
                });
            })
            .Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1));
    })
    .Build();

await host.RunAsync();
