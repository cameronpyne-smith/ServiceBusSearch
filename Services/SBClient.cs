using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using ServiceBusSearch.Models;
using Newtonsoft.Json.Linq;

namespace ServiceBusSearch.Services;

public class SBClient : ISBClient
{
    private readonly AppSettings _appSettings;

    public SBClient(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<ICollection<CloudEventRequest>> PeekDLQ(string name, int quantity)
    {
        // TODO: inject service bus client 
        await using var client = new ServiceBusClient(_appSettings.ServiceBusConnectionString);
        var deadLetterReceiver = client.CreateReceiver($"{name}/$deadletterqueue");

        var messages = await PeekMessages(deadLetterReceiver, quantity);

        List<CloudEventRequest> requests = messages
            .Select(msg => JsonConvert.DeserializeObject<CloudEventRequest>(Encoding.UTF8.GetString(msg.Body)))
            .ToList();
        return requests;
    }

    // TODO: Make try delete messages, try catch and success/failure response
    public async Task DeleteMessage(string queueName, ServiceBusReceivedMessage message)
    {
        await using var client = new ServiceBusClient(_appSettings.ServiceBusConnectionString);
        // TODO: Make configurable if queue or DLQ
        var deadLetterReceiver = client.CreateReceiver($"{queueName}/$deadletterqueue");

        await deadLetterReceiver.CompleteMessageAsync(message);
    }

    // TODO: TBC: Seems like it would be better to defer the messages to be deleted
    // but this wouldn't work if there were existing deffered messages that we don't want to delete (could check condition again?)
    public async Task DeleteMessage(string queueName, string queryPath, string queryValue)
    {
        // TODO: inject service bus client 
        await using var client =
            new ServiceBusClient(_appSettings.ServiceBusConnectionString);

        var receiver = client.CreateReceiver(
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

        const int restoreBatchSize = 50;
        foreach (var batch in deferredSequenceNumbers.Chunk(restoreBatchSize))
        {
            var deferredMessages =
                await receiver.ReceiveDeferredMessagesAsync(batch);

            foreach (var message in deferredMessages)
            {
                await receiver.AbandonMessageAsync(message);
            }
        }

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

    // TODO: Merge this and above to one function
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

    // TODO: Needs cleanup
    public async Task UndeferAllMessages(string queueName)
    {
        await using var client =
            new ServiceBusClient(_appSettings.ServiceBusConnectionString);

        var receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = SubQueue.DeadLetter
            });

        var sender = client.CreateSender(queueName);

        var sequenceNumbers = new List<long>();
        long? fromSequence = null;

        // 1️⃣ Peek all messages
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

        // 2️⃣ Re-enqueue and delete deferred originals
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

}
