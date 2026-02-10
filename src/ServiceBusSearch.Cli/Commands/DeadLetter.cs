using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Core.Services;

namespace ServiceBusSearch.Cli.Commands;

public class DeadLetter : AsyncCommand<DeadLetter.Settings>
{
    private readonly ISBClient _serviceBus;

    public DeadLetter(ISBClient serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _serviceBus.DeadLetterAllMessages(settings.Queue);
        return 0;
    }
}
