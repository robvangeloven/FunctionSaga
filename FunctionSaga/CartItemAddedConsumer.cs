namespace FunctionSaga
{
    using System.Threading.Tasks;

    using MassTransit;

    using Shopping.Contracts;

    public class CartItemAddedConsumer : IConsumer<CartItemAdded>
    {
        public async Task Consume(ConsumeContext<CartItemAdded> context)
        {
            LogContext.Info?.Log("Order submitted for: {UserName}", context.Message.UserName);
        }
    }
}
