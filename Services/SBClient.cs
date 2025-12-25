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

    public async Task GetDeadLetter(string queueName)
    {
        await using var client = new ServiceBusClient(_appSettings.ServiceBusConnectionString);
        var deadLetterReceiver = client.CreateReceiver($"{queueName}/$deadletterqueue");
        var msg = await deadLetterReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
        Console.WriteLine(msg.Body);
    }

    public async Task<ICollection<CloudEventRequest>> PeekDLQ(string name, int quantity)
    {
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
}
