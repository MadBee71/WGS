namespace WGS.Models;

public class ServerNetworkStats
{
    public string ServerId        { get; init; } = string.Empty;
    public int    ConnectionCount { get; set; }
    public double BytesInPerSec   { get; set; }
    public double BytesOutPerSec  { get; set; }

    // Viimeiset 60 näytettä (2 min @ 2 s välein) sparklinea varten
    public Queue<double> HistoryIn  { get; } = new(60);
    public Queue<double> HistoryOut { get; } = new(60);

    public void PushHistory(double bytesIn, double bytesOut)
    {
        if (HistoryIn.Count  >= 60) HistoryIn.Dequeue();
        if (HistoryOut.Count >= 60) HistoryOut.Dequeue();
        HistoryIn.Enqueue(bytesIn);
        HistoryOut.Enqueue(bytesOut);
    }
}
