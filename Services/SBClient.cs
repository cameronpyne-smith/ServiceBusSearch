using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using ServiceBusSearch.Models;
using Newtonsoft.Json.Linq;

namespace ServiceBusSearch.Services;

public class SBClient : ISBClient
{
    private readonly ServiceBusClient _serviceBusClient;

    public SBClient(ServiceBusClient serviceBusClient)
    {
        _serviceBusClient = serviceBusClient;
    }

    public async Task<ICollection<CloudEventRequest>> Peek(string queueName, int quantity, bool isMainQueue)
    {
        var receiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = isMainQueue ? SubQueue.None : SubQueue.DeadLetter,
                PrefetchCount = 20
            });

        var messages = await PeekMessages(receiver, quantity);

        List<CloudEventRequest> requests = messages
            .Select(msg => JsonConvert.DeserializeObject<CloudEventRequest>(Encoding.UTF8.GetString(msg.Body)))
            .ToList();
        return requests;
    }

    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessages(string queueName, bool isMainQueue)
    {
        var receiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                SubQueue = isMainQueue
                    ? SubQueue.None
                    : SubQueue.DeadLetter
            });

        var messages = await receiver.PeekMessagesAsync(50);
        return messages;
    }

    public async Task Send(string queueName, ServiceBusMessage message)
    {
        var sender = _serviceBusClient.CreateSender(queueName);
        await sender.SendMessageAsync(message);
    }

    public async Task DeleteMessage(string queueName, string queryPath, string queryValue)
    {
        var receiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = SubQueue.DeadLetter,
                PrefetchCount = 20
            });

        var deferredSequenceNumbers = new List<long>();
        int deleted = 0;
        int inspected = 0;

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(
                maxMessages: 10,
                maxWaitTime: TimeSpan.FromSeconds(2));

            if (messages.Count == 0)
                break;

            foreach (var message in messages)
            {
                inspected++;

                bool matches = false;

                try
                {
                    var json = JObject.Parse(message.Body.ToString());
                    matches =
                        json.SelectToken(queryPath)?.ToString()
                        == queryValue;
                }
                catch (Exception e)
                {
                }

                if (matches)
                {
                    await receiver.CompleteMessageAsync(message);
                    deleted++;
                }
                else
                {
                    await receiver.DeferMessageAsync(message);
                    deferredSequenceNumbers.Add(message.SequenceNumber);
                }
            }
        }

        await UndeferAllMessages(queueName);
        await DeadLetterAllMessages(queueName);

        await receiver.CloseAsync();
    }

    private async Task<List<ServiceBusReceivedMessage>> PeekMessages(ServiceBusReceiver receiver)
    {
        var messages = new List<ServiceBusReceivedMessage>();
        long? sequenceNumber = null;
        var batchSize = 250;

        while (true)
        {
            var batch = await receiver.PeekMessagesAsync(batchSize, sequenceNumber);
            if (batch.Count == 0) break;
            messages.AddRange(batch);
            sequenceNumber = batch.Last().SequenceNumber + 1;
        }

        return messages;
    }

    private async Task<List<ServiceBusReceivedMessage>> PeekMessages(ServiceBusReceiver receiver, int quantity)
    {
        var messages = new List<ServiceBusReceivedMessage>();
        long? sequenceNumber = null;
        var batchSize = 250;

        while (true)
        {
            if (messages.Count + batchSize > quantity) batchSize = quantity - messages.Count;
            var batch = await receiver.PeekMessagesAsync(batchSize, sequenceNumber);
            if (batch.Count == 0) break;
            messages.AddRange(batch);
            if (messages.Count >= quantity) break;
            sequenceNumber = batch.Last().SequenceNumber + 1;
        }

        return messages;
    }

    public async Task UndeferAllMessages(string queueName)
    {
        var receiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = SubQueue.DeadLetter
            });

        var sender = _serviceBusClient.CreateSender(queueName);

        var sequenceNumbers = new List<long>();
        long? fromSequence = null;

        while (true)
        {
            var peeked = await receiver.PeekMessagesAsync(
                100,
                fromSequence);

            if (peeked.Count == 0)
                break;

            sequenceNumbers.AddRange(peeked.Select(m => m.SequenceNumber));
            fromSequence = peeked.Last().SequenceNumber + 1;
        }

        Console.WriteLine($"Found {sequenceNumbers.Count} deferred messages.");

        int restored = 0;

        foreach (var seq in sequenceNumbers)
        {
            try
            {
                var deferred = await receiver.ReceiveDeferredMessageAsync(seq);
                if (deferred == null)
                    continue;

                var clone = new ServiceBusMessage(deferred.Body)
                {
                    ContentType = deferred.ContentType,
                    CorrelationId = deferred.CorrelationId,
                    Subject = deferred.Subject
                };

                foreach (var prop in deferred.ApplicationProperties)
                    clone.ApplicationProperties[prop.Key] = prop.Value;

                await sender.SendMessageAsync(clone);
                await receiver.CompleteMessageAsync(deferred);

                restored++;
            }
            catch (ServiceBusException ex)
                when (ex.Reason == ServiceBusFailureReason.MessageNotFound)
            {
                // Not deferred — ignore
            }
        }

        Console.WriteLine($"Restored {restored} messages.");

        await receiver.CloseAsync();
    }

    public async Task DeadLetterAllMessages(string queueName)
    {
        var receiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        int deadLettered = 0;

        Console.WriteLine("Dead-lettering messages...");

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(
                maxMessages: 50,
                maxWaitTime: TimeSpan.FromSeconds(2));

            if (messages.Count == 0)
                break;

            foreach (var message in messages)
            {
                await receiver.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "AdminDeadLetter",
                    deadLetterErrorDescription: "Moved by CLI admin command");

                deadLettered++;
            }
        }

        Console.WriteLine($"Dead-lettered {deadLettered} messages.");

        await receiver.CloseAsync();
    }
}
