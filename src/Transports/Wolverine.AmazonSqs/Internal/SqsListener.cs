using System.Text;
using Amazon.SQS.Model;
using Baseline;
using Microsoft.Extensions.Logging;
using Wolverine.Runtime;
using Wolverine.Transports;

namespace Wolverine.AmazonSqs.Internal;

internal class SqsListener : IListener
{
    private readonly ILogger _logger;
    private readonly AmazonSqsQueue _queue;
    private readonly AmazonSqsTransport _transport;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _task;
    private readonly AmazonSqsMapper _mapper;

    public SqsListener(IWolverineRuntime runtime, AmazonSqsQueue queue, AmazonSqsTransport transport,
        IReceiver receiver)
    {
        if (transport.Client == null) throw new InvalidOperationException("Parent transport has not been initialized");

        _mapper = new AmazonSqsMapper(queue, runtime);
        _logger = runtime.Logger;
        _queue = queue;
        _transport = transport;

        var headers = _mapper.AllHeaders().ToList();

        _task = Task.Run(async () =>
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                // TODO -- harden like crazy

                var request = new ReceiveMessageRequest(_queue.QueueUrl)
                {
                    MessageAttributeNames = headers,
                };

                _queue.ConfigureRequest(request);
                
                var results = await _transport.Client.ReceiveMessageAsync(request, _cancellation.Token);

                if (results.Messages.Any())
                {
                    var envelopes = results.Messages.Select(buildEnvelope)
                        .ToArray();

                    // ReSharper disable once CoVariantArrayConversion
                    await receiver.ReceivedAsync(this, envelopes);
                }
                
                // TODO -- harden all of this
                // TODO -- put a cooldown here? 

            }
        }, _cancellation.Token);
    }

    private AmazonSqsEnvelope buildEnvelope(Message message)
    {
        var envelope = new AmazonSqsEnvelope(message);
        _mapper.MapIncomingToEnvelope(envelope, message);
        envelope.Data = Encoding.Default.GetBytes(message.Body);
        return envelope;
    }

    public async ValueTask CompleteAsync(Envelope envelope)
    {
        if (envelope is AmazonSqsEnvelope e)
        {
            await CompleteAsync(e.SqsMessage);
        }
    }

    public async ValueTask DeferAsync(Envelope envelope)
    {
        if (envelope is AmazonSqsEnvelope e)
        {
            await DeferAsync(e.SqsMessage);
        }
    }

    public ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _task.SafeDispose();
        return ValueTask.CompletedTask;
    }

    public Uri Address => _queue.Uri;
    public ValueTask StopAsync()
    {
        return DisposeAsync();
    }

    public Task CompleteAsync(Message sqsMessage)
    {
        // TODO -- harden this like crazy
        return _transport.Client!.DeleteMessageAsync(_queue.QueueUrl, sqsMessage.ReceiptHandle);
    }

    public Task DeferAsync(Message sqsMessage)
    {
        // TODO -- harden this like crazy
        // TODO -- the visibility timeout needs to be configurable by timeout
        return _transport.Client!.ChangeMessageVisibilityAsync(_queue.QueueUrl, sqsMessage.ReceiptHandle, 1000);
    }
}