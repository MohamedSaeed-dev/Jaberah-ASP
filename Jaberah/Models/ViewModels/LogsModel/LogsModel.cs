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
    public DateTime Timestamp { get; set; }
    public string Method { get; set; }
    public string Url { get; set; }
    public string Body { get; set; }
    public int StatusCode { get; set; }
    public string Response { get; set; }
}
