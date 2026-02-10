using Azure.Messaging.ServiceBus;
using ServiceBusSearch.Core.Models;

namespace ServiceBusSearch.Core.Services;

public interface ISBClient
{
    // READ
    Task<ICollection<CloudEventRequest>> Peek(string queueName, int quantity, bool isMainQueue);
    Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessages(string queueName, bool isMainQueue);

    // SEND
    Task Send(string queueName, ServiceBusMessage message);

    // DELETE
    Task DeleteMessage(string queueName, string queryPath, string queryValue);

    // DEFER
    Task UndeferAllMessages(string queueName);

    // DEADLETTER
    Task DeadLetterAllMessages(string queueName);
}
