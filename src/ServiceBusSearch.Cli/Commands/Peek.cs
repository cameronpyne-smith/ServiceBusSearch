using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using System.ComponentModel;
using ServiceBusSearch.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ServiceBusSearch.Cli.Commands;

public class Peek : AsyncCommand<Peek.Settings>
{
    private readonly ISBClient _sbClient;

    public Peek(ISBClient sbClient)
    {
        _sbClient = sbClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = string.Empty;

        [CommandOption("--max <MAX>")]
        [Description("The maximum number of messages to peek from the queue")]
        public int Max { get; set; } = 100;

        [CommandOption("--json <JSON>")]
        [Description("Print the messages in json")]
        public bool Json { get; set; }

        [CommandOption("--table <TABLE>")]
        [Description("Print the messages as a table")]
        public bool Table { get; set; }

        [CommandOption("--correlationId <CORRELATION_ID>")]
        [Description("Filter for messages with a matching correlation id")]
        public string CorrelationId { get; set; } = string.Empty;

        [CommandOption("--where <WHERE>")]
        [Description("Filter messages by query")]
        public string Where { get; set; } = string.Empty;

        [CommandOption("--mainQueue")]
        [Description("Switch from the dead letter queue to the main queue")]
        public bool IsMainQueue { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var msgs = await _sbClient.Peek(settings.Queue, settings.Max, settings.IsMainQueue);

        if (!string.IsNullOrEmpty(settings.CorrelationId))
        {
            msgs = msgs.Where(msg => msg.Data?["CorrelationId"]?.ToString() == settings.CorrelationId).ToList();
        }

        if (!string.IsNullOrEmpty(settings.Where))
        {
            var path = settings.Where.Split("=")[0];
            var query = settings.Where.Split("=")[1];
            msgs = msgs.Where(msg =>
            {
                var root = JObject.FromObject(msg);
                var token = root.SelectToken(path);
                return token?.ToString() == query;
            }).ToList();
        }

        if (settings.Json)
        {
            var json = JsonConvert.SerializeObject(
                msgs,
                Formatting.Indented);

            var jsonText = new JsonText(json)
                .MemberColor(Color.Purple)
                .StringColor(Color.Green)
                .NumberColor(Color.DarkOrange)
                .BooleanColor(Color.Blue);
            AnsiConsole.Write(jsonText);
        }

        if (settings.Table)
        {
            var table = new Table();
            table.AddColumn("Id");
            table.AddColumn("Type");
            foreach (var msg in msgs)
            {
                table.AddRow(msg.Id, msg.Type);
            }
            AnsiConsole.Write(table);
        }

        return 0;
    }
}
