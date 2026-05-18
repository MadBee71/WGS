using CommunityToolkit.Mvvm.ComponentModel;

namespace WGS.ViewModels;

public partial class CpuCoreItem : ObservableObject
{
    public int Index { get; }
    public string Label => $"Core {Index}";

    [ObservableProperty] private bool _isEnabled;

    public event Action? Changed;

    public CpuCoreItem(int index, bool enabled)
    {
        Index = index;
        _isEnabled = enabled;
    }

    partial void OnIsEnabledChanged(bool value) => Changed?.Invoke();
}
