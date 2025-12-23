using ServiceBusSearch.Models;

namespace ServiceBusSearch.Services;

public interface IServiceBus
{
    public Task GetDeadLetter(string queueName);
    public Task<ICollection<CloudEventRequest>> PeekDLQ(string name, int quantity);
}
