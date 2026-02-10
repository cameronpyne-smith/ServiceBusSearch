using Newtonsoft.Json.Linq;

namespace ServiceBusSearch.Core.Models;

public class CloudEventRequest
{
    public string Type { get; set; } = string.Empty;
    public Uri? Source { get; set; }
    public string Id { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public string? DataContentType { get; set; }
    public Uri? DataSchema { get; set; }
    public JObject? Data { get; set; }
}
