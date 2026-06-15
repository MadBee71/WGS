namespace WGS;

public static class AppInfo
{
    public static string Version =>
        System.Reflection.Assembly.GetExecutingAssembly()
              .GetName().Version?.ToString(3) ?? "?";

    public static string VersionTag => $"v{Version}  ·  2026 © MadBee71";
}
