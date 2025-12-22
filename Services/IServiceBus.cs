namespace ServiceBusSearch.Services;

public interface IServiceBus
{
    public Task GetDeadLetter(string queueName);
}
