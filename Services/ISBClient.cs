using ServiceBusSearch.Models;

namespace ServiceBusSearch.Services;

public interface ISBClient
{
    public Task GetDeadLetter(string queueName);
    public Task<ICollection<CloudEventRequest>> PeekDLQ(string name, int quantity);
}
