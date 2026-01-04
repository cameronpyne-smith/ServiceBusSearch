using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

using ServiceBusSearch.Services;

namespace ServiceBusSearch.Commands;

public class Stats : AsyncCommand<Stats.Settings>
{
    private readonly ISBClient _serviceBus;

    public Stats(ISBClient serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--mainQueue")]
        [Description("Switch from the dead letter queue to the main queue")]
        public bool IsMainQueue { get; set; }

        [CommandOption("--max <MAX>")]
        [Description("The maximum number of messages to peek from the queue (default: 100)")]
        public int Max { get; set; } = 100;

        [CommandOption("--order <ORDER>")]
        [Description("Order by count decending (default: false)")]
        public bool Order { get; set; } = false;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Peeking DLQ of: [bold blue]{settings.Queue}[/]!");
        var msgs = await _serviceBus.Peek(settings.Queue, settings.Max, settings.IsMainQueue);

        var groups = msgs.GroupBy(msg => msg.Type);
        if (settings.Order) groups = groups.OrderByDescending(group => group.Count());
        var random = new Random();

        AnsiConsole.Write(new BarChart()
            .Width(100)
            .Label($"[green bold underline]Request Types[/]\n(Total: [yellow bold]{msgs.Count}[/])")
            .CenterLabel()
            .AddItems(groups, (group) => new BarChartItem(group.Key, group.Count(), Color.FromInt32(random.Next(1, 231)))));

        return 0;
    }
}
