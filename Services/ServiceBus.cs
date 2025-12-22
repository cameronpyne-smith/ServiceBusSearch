using Azure.Messaging.ServiceBus;

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
}
