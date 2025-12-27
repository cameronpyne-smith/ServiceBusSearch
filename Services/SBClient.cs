using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using ServiceBusSearch.Models;

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

        var allMessages = new List<ServiceBusReceivedMessage>();
        long? sequenceNumber = null;
        var batchSize = 250;

        while (true)
        {
            if (allMessages.Count + batchSize > quantity) batchSize = quantity - allMessages.Count;
            var batch = await deadLetterReceiver.PeekMessagesAsync(batchSize, sequenceNumber);
            if (batch.Count == 0) break;
            allMessages.AddRange(batch);
            if (allMessages.Count >= quantity) break;
            sequenceNumber = batch.Last().SequenceNumber + 1;
        }

        List<CloudEventRequest> requests = allMessages
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

    public async Task DeleteMessage(string queueName, string correlationId)
    { }

    public async Task DeleteMessage(string queueName, string queryPath, string queryValue)
    { }
}
