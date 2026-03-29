using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using LTTPEnhancementTools.Services;
using Microsoft.Win32;

namespace LTTPEnhancementTools;

public partial class SetupWizardWindow : Window, INotifyPropertyChanged
{
    private int _step;
    private readonly StackPanel[] _stepPanels;

    // Properties bound to the wizard TextBoxes
    private string? _emulatorPath;
    private string? _connectorScriptPath;
    private string? _sniPath;
    private string? _archipelagoLauncherPath;
    private string? _trackerUrl;

    public string? EmulatorPath
    {
        get => _emulatorPath;
        set { _emulatorPath = value; OnPropertyChanged(); }
    }
    public string? ConnectorScriptPath
    {
        get => _connectorScriptPath;
        set { _connectorScriptPath = value; OnPropertyChanged(); }
    }
    public string? SniPath
    {
        get => _sniPath;
        set { _sniPath = value; OnPropertyChanged(); }
    }
    public string? ArchipelagoLauncherPath
    {
        get => _archipelagoLauncherPath;
        set { _archipelagoLauncherPath = value; OnPropertyChanged(); }
    }
    public string? TrackerUrl
    {
        get => _trackerUrl;
        set { _trackerUrl = value; OnPropertyChanged(); }
    }

    // Result available after dialog closes
    public LaunchSettings? Result { get; private set; }

    public SetupWizardWindow(LaunchSettings? existing = null)
    {
        InitializeComponent();
        DataContext = this;

        // Pre-populate from existing settings (re-run wizard scenario)
        if (existing is not null)
        {
            EmulatorPath        = existing.EmulatorPath.NullIfEmpty();
            ConnectorScriptPath = existing.ConnectorScriptPath.NullIfEmpty();
            SniPath             = existing.SniPath.NullIfEmpty();
            ArchipelagoLauncherPath = existing.ArchipelagoLauncherPath.NullIfEmpty();
            TrackerUrl          = existing.TrackerUrl.NullIfEmpty();
        }

        _stepPanels = [Step0, Step1, Step2, Step3, Step4];
        ShowStep(0);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Sync tracker ComboBox to pre-populated value
        foreach (ComboBoxItem item in WizardTrackerCombo.Items)
        {
            if ((item.Tag as string) == (_trackerUrl ?? string.Empty))
            {
                WizardTrackerCombo.SelectedItem = item;
                return;
            }
        }
        WizardTrackerCombo.SelectedIndex = 0;
    }

    private void ShowStep(int step)
    {
        _step = step;
        for (int i = 0; i < _stepPanels.Length; i++)
            _stepPanels[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content    = step == _stepPanels.Length - 1 ? "Finish ✓" : "Next →";
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step < _stepPanels.Length - 1)
        {
            ShowStep(_step + 1);
        }
        else
        {
            SaveAndClose();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        // Skip without saving — Result stays null
        DialogResult = false;
    }

    private void SaveAndClose()
    {
        Result = new LaunchSettings
        {
            EmulatorPath        = _emulatorPath        ?? string.Empty,
            ConnectorScriptPath = _connectorScriptPath ?? string.Empty,
            SniPath             = _sniPath             ?? string.Empty,
            ArchipelagoLauncherPath = _archipelagoLauncherPath ?? string.Empty,
            TrackerUrl          = _trackerUrl          ?? string.Empty,
        };
        LaunchSettingsManager.Save(Result);
        DialogResult = true;
    }

    // Browse handlers
    private void BrowseEmulator_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Emulator EXE",
            Filter = "EXE (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true) EmulatorPath = dlg.FileName;
    }

    private void BrowseConnector_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Connector Script",
            Filter = "Lua Script (*.lua)|*.lua|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true) ConnectorScriptPath = dlg.FileName;
    }

    private void BrowseSni_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select SNI.exe",
            Filter = "EXE (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true) SniPath = dlg.FileName;
    }

    private void BrowseArchipelago_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select ArchipelagoLauncher.exe",
            Filter = "EXE (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true) ArchipelagoLauncherPath = dlg.FileName;
    }

    private void WizardTrackerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WizardTrackerCombo.SelectedItem is ComboBoxItem item)
            TrackerUrl = item.Tag as string;
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) => string.IsNullOrEmpty(s) ? null : s;
}
