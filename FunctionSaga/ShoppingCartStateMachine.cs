namespace FunctionSaga;

using System;

using MassTransit;

using Microsoft.Extensions.Logging;

using Shopping.Contracts;

public class ShoppingCartStateMachine : MassTransitStateMachine<ShoppingCart>
{
    private readonly ILogger<ShoppingCartStateMachine> _logger;

    public ShoppingCartStateMachine(ILogger<ShoppingCartStateMachine> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Event(() => ItemAdded, x => x.CorrelateBy(cart => cart.UserName!, context => context.Message.UserName)
            .SelectId(context => Guid.NewGuid()));

        Event(() => Submitted, x => x.CorrelateById(context => context.Message.CartId));

        Schedule(() => CartExpired, x => x.ExpirationId, x =>
        {
            x.Delay = TimeSpan.FromSeconds(10);
            x.Received = e => e.CorrelateById(context => context.Message.CartId);
        });

        Initially(
            When(ItemAdded)
                .Then(context =>
                {
                    context.Saga.Created = context.Message.Timestamp;
                    context.Saga.Updated = context.Message.Timestamp;
                    context.Saga.UserName = context.Message.UserName;
                })
                .ThenAsync(context => Console.Out.WriteLineAsync($"Item Added: {context.Message.UserName} to {context.Saga.CorrelationId}"))
                .Schedule(CartExpired, context => new CartExpiredEvent(context.Saga))
                .TransitionTo(Active)
            );

        During(Active,
            When(Submitted)
                .Then(context =>
                {
                    if (context.Message.Timestamp > context.Saga.Updated)
                        context.Saga.Updated = context.Message.Timestamp;

                    context.Saga.OrderId = context.Message.OrderId;
                })
                .ThenAsync(context => Console.Out.WriteLineAsync($"Cart Submitted: {context.Message.UserName} to {context.Saga.CorrelationId}"))
                .Unschedule(CartExpired)
                .TransitionTo(Ordered),
            When(ItemAdded)
                .Then(context =>
                {
                    if (context.Message.Timestamp > context.Saga.Updated)
                        context.Saga.Updated = context.Message.Timestamp;
                })
                .ThenAsync(context => Console.Out.WriteLineAsync($"Item Added: {context.Message.UserName} to {context.Saga.CorrelationId}"))
                .Schedule(CartExpired, context => new CartExpiredEvent(context.Saga)),
            When(CartExpired!.Received)
                .ThenAsync(context => Console.Out.WriteLineAsync($"Item Expired: {context.Saga.CorrelationId}"))
                .Publish(context => new CartRemovedEvent(context.Saga))
                .Finalize()
            );

        SetCompletedWhenFinalized();
    }


    public State Active { get; private set; }
    public State Ordered { get; private set; }

    public Schedule<ShoppingCart, CartExpired> CartExpired { get; private set; }

    public Event<CartItemAdded> ItemAdded { get; private set; }
    public Event<OrderSubmitted> Submitted { get; private set; }


    class CartExpiredEvent :
        CartExpired
    {
        readonly ShoppingCart _instance;

        public CartExpiredEvent(ShoppingCart instance)
        {
            _instance = instance;
        }

        public Guid CartId => _instance.CorrelationId;
    }


    class CartRemovedEvent :
        CartRemoved
    {
        readonly ShoppingCart _instance;

        public CartRemovedEvent(ShoppingCart instance)
        {
            _instance = instance;
        }

        public Guid CartId => _instance.CorrelationId;
        public string UserName => _instance.UserName;
    }
}
