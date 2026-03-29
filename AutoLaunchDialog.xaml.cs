using System.Windows;

namespace LTTPEnhancementTools;

public enum AutoLaunchOption { None, TrackerOnly, ArchipelagoOnly, ArchipelagoAndTracker }

public partial class AutoLaunchDialog : Window
{
    public AutoLaunchOption SelectedOption { get; private set; } = AutoLaunchOption.None;

    public AutoLaunchDialog(bool hasTracker, bool hasArchipelago)
    {
        InitializeComponent();

        TrackerOnlyBtn.IsEnabled = hasTracker;
        ArchipelagoOnlyBtn.IsEnabled = hasArchipelago;
        ArchipelagoAndTrackerBtn.IsEnabled = hasTracker || hasArchipelago;
    }

    private void TrackerOnly_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = AutoLaunchOption.TrackerOnly;
        DialogResult = true;
    }

    private void ArchipelagoOnly_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = AutoLaunchOption.ArchipelagoOnly;
        DialogResult = true;
    }

    private void ArchipelagoAndTracker_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = AutoLaunchOption.ArchipelagoAndTracker;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
