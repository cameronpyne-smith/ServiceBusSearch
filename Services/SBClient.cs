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
    public async Task DeleteMessage(string queueName, string correlationId)
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
                        json.SelectToken("$.Data.CorrelationId")?.ToString()
                        == correlationId;
                }
                catch { }

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

    public async Task DeleteMessage(string queueName, string queryPath, string queryValue)
    {
        // TODO: inject service bus client 
        await using var client = new ServiceBusClient(_appSettings.ServiceBusConnectionString);
        var deadLetterReceiver = client.CreateReceiver($"{queueName}/$deadletterqueue");

        var messages = await PeekMessages(deadLetterReceiver);
        foreach (var message in messages)
        {
            var json = JObject.Parse(message.Body.ToString());
            if (json.SelectToken(queryPath)?.ToString() == queryValue)
            {
                await deadLetterReceiver.CompleteMessageAsync(message);
            }
        }
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
}
