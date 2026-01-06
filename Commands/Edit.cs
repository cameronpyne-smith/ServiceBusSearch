using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Messaging.ServiceBus;
using System.Diagnostics;

namespace ServiceBusSearch.Commands;

public class Edit : AsyncCommand<Edit.Settings>
{
    private readonly ISBClient _sbClient;

    public Edit(ISBClient sbClient)
    {
        _sbClient = sbClient;
    }

    public class Settings : CommandSettings
    {
        // TODO: Make all strings nullable, don't default to empty, it's not as clear
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--max <MAX>")]
        [Description("The maximum number of messages to peek from the queue")]
        public int Max { get; set; } = 100;

        [CommandOption("--mainQueue")]
        [Description("Switch from the dead letter queue to the main queue")]
        public bool IsMainQueue { get; set; }
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var messages = await _sbClient.PeekMessages(settings.Queue, settings.IsMainQueue);

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No messages found.[/]");
            return 0;
        }

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<ServiceBusReceivedMessage>()
                .Title("Select a message to clone & edit")
                .PageSize(10)
                .UseConverter(m =>
                    $"{m.SequenceNumber} | {m.Subject ?? "(no subject)"} | {m.CorrelationId}")
                .AddChoices(messages));

        var json = JObject.Parse(selected.Body.ToString());

        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"sb-edit-{selected.SequenceNumber}.json");

        await File.WriteAllTextAsync(
            tempFile,
            json.ToString(Formatting.Indented));

        var editor = "vim";

        AnsiConsole.MarkupLine($"[grey]Opening editor: {editor}[/]");

        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = editor,
            Arguments = tempFile,
            UseShellExecute = false
        }))
        {
            process?.WaitForExit();
        }

        var editedJson = JObject.Parse(
            await File.ReadAllTextAsync(tempFile));

        var newMessage = new ServiceBusMessage(
            BinaryData.FromString(
                editedJson.ToString(Formatting.None)))
        {
            ContentType = selected.ContentType,
            Subject = selected.Subject,
            CorrelationId = selected.CorrelationId
        };

        foreach (var kv in selected.ApplicationProperties)
        {
            newMessage.ApplicationProperties[kv.Key] = kv.Value;
        }

        newMessage.ApplicationProperties["clonedFromSequence"] =
            selected.SequenceNumber;

        await _sbClient.Send(settings.Queue, newMessage);

        AnsiConsole.MarkupLine(
            $"[green]✔ Message cloned and sent successfully.[/]");

        return 0;
    }
}

record MessageCandidate(
    ServiceBusReceivedMessage Message,
    string Label,
    string? CorrelationId
);
