namespace WGS.Models;

public class MachineDefinition
{
    public string Id      { get; set; } = Guid.NewGuid().ToString();
    public string Name    { get; set; } = "";
    public string Url     { get; set; } = ""; // e.g. http://192.168.1.50:8765
    public string Token   { get; set; } = "";
    public string Color   { get; set; } = "#1f6feb";
    public bool   Enabled { get; set; } = true;
}
