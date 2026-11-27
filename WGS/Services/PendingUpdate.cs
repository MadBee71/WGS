namespace WGS.Services;

/// <summary>
/// An update that is waiting for the server to empty ("Only update when the server is empty"). While it waits, the
/// window and the background service look for an empty server every <see cref="RecheckSeconds"/> instead of only at
/// the next update check, and can tell the players about it.
/// </summary>
public static class PendingUpdate
{
    public const int RecheckSeconds = 60;
    public static readonly TimeSpan NoticeRepeat = TimeSpan.FromMinutes(15);
    public const string PlayerNotice = "A server update is available. The server will update once everyone has left.";
}
