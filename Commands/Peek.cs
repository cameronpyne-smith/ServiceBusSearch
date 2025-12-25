using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using System.ComponentModel;
using ServiceBusSearch.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ServiceBusSearch.Commands;

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
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--max <MAX>")]
        [Description("The maximum number of messages to peek from the queue")]
        public int Max { get; set; } = 100;

        [CommandOption("--json <JSON>")]
        [Description("Print the messages in json")]
        public bool Json { get; set; } = false;

        [CommandOption("--table <TABLE>")]
        [Description("Print the messages as a table")]
        public bool Table { get; set; } = false;

        [CommandOption("--correlationId <CORRELATION_ID>")]
        [Description("Filter for messages with a matching correlation id")]
        public string CorrelationId { get; set; } = String.Empty;

        [CommandOption("--where <WHERE>")]
        [Description("Filter messages by query")]
        public string Where { get; set; } = String.Empty;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Reading DLQ of: [bold blue]{settings.Queue}[/]!");
        var msgs = await _sbClient.PeekDLQ(settings.Queue, settings.Max);

        // TODO: Move these to functions
        if (!string.IsNullOrEmpty(settings.CorrelationId))
        {
            msgs = msgs.Where(msg => msg.Data?["CorrelationId"]?.ToString() == settings.CorrelationId).ToList();
        }

        // TODO: Add check with error if both where and correlation id flags are used
        if (!string.IsNullOrEmpty(settings.Where))
        {
            // TODO: Add checks for malformed input
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
                .NumberColor(Color.DarkOrange)
                .StringColor(Color.Green);
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
