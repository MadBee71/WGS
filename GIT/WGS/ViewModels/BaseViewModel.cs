using CommunityToolkit.Mvvm.ComponentModel;
using WGS.Services;

namespace WGS.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    public LocalizationService Loc => LocalizationService.Instance;
}
