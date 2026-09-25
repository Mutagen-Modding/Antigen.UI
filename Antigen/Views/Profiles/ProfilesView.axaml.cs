using Antigen.ViewModels.Profiles;
using Avalonia.Controls;

namespace Antigen.Views.Profiles;

public partial class ProfilesView : UserControl
{
    public ProfilesVM? ViewModel => DataContext as ProfilesVM;

    public ProfilesView()
    {
        InitializeComponent();
    }
}
