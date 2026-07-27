namespace WGS.Models;

public class ServerNetworkStats
{
    public string ServerId        { get; init; } = string.Empty;
    public int    ConnectionCount { get; set; }
    public double BytesInPerSec   { get; set; }
    public double BytesOutPerSec  { get; set; }

    // Last 60 samples (2 min @ 2 s interval) for the sparkline
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
