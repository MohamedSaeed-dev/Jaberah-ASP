using System;
using System.Text.Json.Serialization;

public class LogEntry
{
    public DateTime Timestamp { get; set; }

    public string Level { get; set; }

    public string MessageTemplate { get; set; }

    public Properties Properties { get; set; }
}

public class Properties
{
    public HttpLog HttpLog { get; set; }
}

public class HttpLog
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("response")]
    public string Response { get; set; }
}
