using ServiceBusSearch.Models;

namespace ServiceBusSearch.Services;

public interface ISBClient
{
    // READ
    public Task<ICollection<CloudEventRequest>> Peek(string queueName, int quantity, bool isMainQueue);

    // DELETE
    public Task DeleteMessage(string queueName, string queryPath, string queryValue);

    // DEFER
    public Task UndeferAllMessages(string queueName);

    // DEADLETTER
    public Task DeadLetterAllMessages(string queueName);
}
