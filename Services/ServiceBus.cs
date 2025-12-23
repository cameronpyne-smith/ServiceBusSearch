using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using ServiceBusSearch.Models;

namespace ServiceBusSearch.Services;

public class ServiceBus : IServiceBus
{
    private readonly AppSettings _appSettings;

    public ServiceBus(AppSettings appSettings)
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
        var deadLetters = await deadLetterReceiver.PeekMessagesAsync(quantity);
        List<CloudEventRequest> requests = deadLetters.Select(msg => JsonConvert.DeserializeObject<CloudEventRequest>(Encoding.UTF8.GetString(msg.Body))).ToList();
        return requests;
    }
}
