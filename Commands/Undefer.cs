using Spectre.Console.Cli;
using System.ComponentModel;

using ServiceBusSearch.Services;

namespace ServiceBusSearch.Commands;

public class Undefer : AsyncCommand<Undefer.Settings>
{
    private readonly ISBClient _serviceBus;

    public Undefer(ISBClient serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _serviceBus.UndeferAllMessages(settings.Queue);
        return 0;
    }
}
