using ModelContextProtocol.Server;
using ServiceBusSearch.Core.Services;
using System.ComponentModel;
using Newtonsoft.Json;

namespace ServiceBusSearch.McpServer.McpTools;

public class ServiceBusTools
{
    private readonly ISBClient _sbClient;

    public ServiceBusTools(ISBClient sbClient)
    {
        _sbClient = sbClient;
    }

    [McpServerTool, Description("Peek messages in a Service Bus queue or dead-letter queue")]
    public async Task<string> Peek(
        [Description("Queue name")] string queue,
        [Description("Max messages to peek")] int max = 100,
        [Description("Use main queue instead of DLQ")] bool mainQueue = false)
    {
        var msgs = await _sbClient.Peek(queue, max, mainQueue);
        return JsonConvert.SerializeObject(msgs, Formatting.Indented);
    }

    [McpServerTool, Description("Delete messages from the dead-letter queue by filter")]
    public async Task<string> Delete(
        [Description("Queue name")] string queue,
        [Description("Correlation ID to match")] string? correlationId = null,
        [Description("JSONPath query (e.g. $.Data.Property=value)")] string? where = null)
    {
        if (!string.IsNullOrEmpty(correlationId))
        {
            await _sbClient.DeleteMessage(queue, "$.Data.CorrelationId", correlationId);
            return "Deleted messages with matching correlation ID.";
        }
        if (!string.IsNullOrEmpty(where))
        {
            var parts = where.Split("=");
            if (parts.Length != 2)
            {
                return "Invalid 'where' format. Use $.Data.Property=value";
            }
            await _sbClient.DeleteMessage(queue, parts[0], parts[1]);
            return "Deleted messages matching query.";
        }
        return "No filter provided. Specify correlationId or where.";
    }

    [McpServerTool, Description("Move all deferred messages in the DLQ back to the main queue")]
    public async Task<string> Undefer([Description("Queue name")] string queue)
    {
        await _sbClient.UndeferAllMessages(queue);
        return "Undeferred all messages.";
    }

    [McpServerTool, Description("Move all messages in the main queue to the dead-letter queue")]
    public async Task<string> DeadLetter([Description("Queue name")] string queue)
    {
        await _sbClient.DeadLetterAllMessages(queue);
        return "Dead-lettered all messages.";
    }
}
