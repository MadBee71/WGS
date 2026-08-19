namespace WGS.ViewModels;

public sealed class PluginTabVm
{
    public string Key { get; init; } = "";
    public required string Header { get; init; }
    public string? ToolTip { get; init; }
    public int Order { get; init; }
    public required object Content { get; init; }
}
