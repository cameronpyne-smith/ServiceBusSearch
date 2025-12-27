using Azure.Messaging.ServiceBus;
using ServiceBusSearch.Models;

namespace ServiceBusSearch.Services;

public interface ISBClient
{
    // READ
    public Task<ICollection<CloudEventRequest>> PeekDLQ(string queueName, int quantity);

    // DELETE
    public Task DeleteMessage(string queueName, ServiceBusReceivedMessage message);
    public Task DeleteMessage(string queueName, string correlationId);
    public Task DeleteMessage(string queueName, string queryPath, string queryValue);
}
