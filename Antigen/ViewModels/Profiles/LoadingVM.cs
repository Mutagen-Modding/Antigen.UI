namespace Antigen.ViewModels.Profiles;

public sealed class LoadingVM : ResizablePanelVM, ISingleton
{
    public LoadingVM()
    {
        IsExpanded = true;
    }
}
