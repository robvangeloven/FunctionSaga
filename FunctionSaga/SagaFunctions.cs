using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using MassTransit;
using MassTransit.Transports;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using Shopping.Contracts;

namespace FunctionSaga
{
    public class SagaFunctions
    {
        private readonly IReceiveEndpointDispatcher<CartItemAdded> _receiveEndpointDispatcherConsumer;
        private readonly IReceiveEndpointDispatcher<CartItemAdded> _receiveEndpointDispatcherSaga;

        public SagaFunctions(
            IReceiveEndpointDispatcher<CartItemAdded> receiveEndpointDispatcherConsumer,
            IReceiveEndpointDispatcher<CartItemAdded> receiveEndpointDispatcherSaga)
        {
            _receiveEndpointDispatcherConsumer = receiveEndpointDispatcherConsumer ?? throw new ArgumentNullException(nameof(receiveEndpointDispatcherConsumer));
            _receiveEndpointDispatcherSaga = receiveEndpointDispatcherSaga ?? throw new ArgumentNullException(nameof(receiveEndpointDispatcherSaga));
        }

        [Function("HttpConsumerEndpoint")]
        public async Task<HttpResponseData> RunConsumer(
            [HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequestData req,
            FunctionContext context,
            CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();

            req.Body.CopyTo(memoryStream);

            await _receiveEndpointDispatcherConsumer.Dispatch(context, memoryStream.ToArray(), cancellationToken);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("HttpSagaEndpoint")]
        public async Task<HttpResponseData> RunSaga(
            [HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequestData req,
            FunctionContext context,
            CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();

            req.Body.CopyTo(memoryStream);

            await _receiveEndpointDispatcherSaga.Dispatch(context, memoryStream.ToArray(), cancellationToken);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
