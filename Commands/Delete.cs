using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Services;

namespace ServiceBusSearch.Commands;

public class Delete : AsyncCommand<Delete.Settings>
{
    private readonly ISBClient _sbClient;

    public Delete(ISBClient sbClient)
    {
        _sbClient = sbClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--correlationId <CORRELATION_ID>")]
        [Description("Filter for messages with a matching correlation id")]
        public string CorrelationId { get; set; } = String.Empty;

        [CommandOption("--where <WHERE>")]
        [Description("Filter messages by query")]
        public string Where { get; set; } = String.Empty;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // TODO: Move these to functions
        if (!string.IsNullOrEmpty(settings.CorrelationId))
        {
            await _sbClient.DeleteMessage(settings.Queue, settings.CorrelationId);
            return 0;
        }

        if (!string.IsNullOrEmpty(settings.Where))
        {
            // TODO: Add checks for malformed input
            var path = settings.Where.Split("=")[0];
            var query = settings.Where.Split("=")[1];
            await _sbClient.DeleteMessage(settings.Queue, path, query);
            return 0;
        }

        return 1;
    }
}
