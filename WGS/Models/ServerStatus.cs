namespace WGS.Models;

public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Installing,
    Updating,
    Error,
    NotInstalled
}
