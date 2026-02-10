using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Core.Services;

namespace ServiceBusSearch.Cli.Commands;

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
        public string Queue { get; set; } = string.Empty;

        [CommandOption("--correlationId <CORRELATION_ID>")]
        [Description("Filter for messages with a matching correlation id")]
        public string CorrelationId { get; set; } = string.Empty;

        [CommandOption("--where <WHERE>")]
        [Description("Filter messages by query")]
        public string Where { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(settings.CorrelationId) && !string.IsNullOrEmpty(settings.Where))
        {
            AnsiConsole.MarkupLine("[red]You cannot use --correlationId and --where together[/]");
            return 1;
        }

        if (!string.IsNullOrEmpty(settings.CorrelationId))
        {
            await _sbClient.DeleteMessage(settings.Queue, "$.Data.CorrelationId", settings.CorrelationId);
            return 0;
        }

        if (!string.IsNullOrEmpty(settings.Where))
        {
            var split = settings.Where.Split("=");
            if (split.Length != 1 && split.Length != 2)
            {
                AnsiConsole.MarkupLine("[red]--where should be of the format {query}={value} e.g. $.Data.Id=123[/]");
                return 1;
            }

            var path = split[0];
            var query = split.Length == 2 ? split[1] : "";
            await _sbClient.DeleteMessage(settings.Queue, path, query);
            return 0;
        }

        return 1;
    }
}
