namespace WGS.Models;

public enum ConsoleMessageType { Info, Warning, Error, System, Input }

public class ConsoleMessage
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Text { get; set; } = string.Empty;
    public ConsoleMessageType Type { get; set; } = ConsoleMessageType.Info;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
}
