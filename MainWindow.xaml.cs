using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LTTPEnhancementTools.Models;
using LTTPEnhancementTools.Services;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxOptions = System.Windows.MessageBoxOptions;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LTTPEnhancementTools;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ── Observable state ──────────────────────────────────────────────────
    private string? _romPath;
    private string? _romBaseName;
    private string? _outputBaseName;
    private string? _outputDir;
    private string? _spritePath;
    private string? _spritePreviewUrl;
    private string? _defaultSpritePreviewUrl;
    private bool _isApplying;
    private bool _isConverting;
    private string? _emulatorPath;
    private string? _connectorScriptPath;
    private string? _sniPath;
    private string? _archipelagoLauncherPath;
    private string? _trackerUrl;
    private string? _seedUrl;
    private string? _currentPlaylistPath;
    private string? _currentPlaylistName;
    private bool _hasUnsavedPlaylistChanges;
    private bool _isMusicExpanded;
    private bool _isConfigExpanded;
    private bool _isAutoLauncherExpanded;

    public string? EmulatorPath
    {
        get => _emulatorPath;
        set { _emulatorPath = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }
    public string? ConnectorScriptPath
    {
        get => _connectorScriptPath;
        set { _connectorScriptPath = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }
    public string? SniPath
    {
        get => _sniPath;
        set { _sniPath = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }
    public string? ArchipelagoLauncherPath
    {
        get => _archipelagoLauncherPath;
        set { _archipelagoLauncherPath = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }
    public string? TrackerUrl
    {
        get => _trackerUrl;
        set { _trackerUrl = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }
    public string? SeedUrl
    {
        get => _seedUrl;
        set { _seedUrl = value; OnPropertyChanged(); SaveLaunchSettings(); }
    }

    public ObservableCollection<TrackSlot> Tracks { get; } = new();

    public string? RomBaseName
    {
        get => _romBaseName;
        private set { _romBaseName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasRom)); OnPropertyChanged(nameof(CanApply)); }
    }

    public string? OutputBaseName
    {
        get => _outputBaseName;
        set { _outputBaseName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); }
    }

    public string? OutputDir
    {
        get => _outputDir;
        private set { _outputDir = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); }
    }

    public string? SpritePath
    {
        get => _spritePath;
        set
        {
            _spritePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSprite));
            OnPropertyChanged(nameof(SpriteDisplayName));
            OnPropertyChanged(nameof(IsRandomSprite));
            OnPropertyChanged(nameof(RandomGlyph));
            OnPropertyChanged(nameof(CanApply));
            SaveAutoState();
        }
    }

    public string? SpritePreviewUrl
    {
        get => _spritePreviewUrl;
        set { _spritePreviewUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePreviewUrl)); }
    }

    /// <summary>Local path of the default Link Nintendo preview, shown when no custom sprite is selected.</summary>
    public string? DefaultSpritePreviewUrl
    {
        get => _defaultSpritePreviewUrl;
        private set { _defaultSpritePreviewUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePreviewUrl)); }
    }

    /// <summary>The preview to display — custom sprite if set, otherwise the default Link preview.</summary>
    public string? EffectivePreviewUrl => _spritePreviewUrl ?? _defaultSpritePreviewUrl;

    public bool HasSprite => _spritePath is not null;

    public string? SpriteDisplayName => _spritePath switch
    {
        SpriteBrowserWindow.RandomAllSentinel       => "Random (any sprite)",
        SpriteBrowserWindow.RandomFavoritesSentinel => "Random (from favorites)",
        null                                        => null,
        _                                           => Path.GetFileNameWithoutExtension(_spritePath)
    };

    public bool IsRandomSprite =>
        _spritePath == SpriteBrowserWindow.RandomAllSentinel ||
        _spritePath == SpriteBrowserWindow.RandomFavoritesSentinel;

    public string RandomGlyph =>
        _spritePath == SpriteBrowserWindow.RandomFavoritesSentinel ? "?★" : "?";

    public bool HasRom => _romPath is not null;

    public bool CanApply => _romPath is not null
                         && _outputDir is not null
                         && (Tracks.Any(t => t.HasFile) || HasSprite)
                         && !string.IsNullOrWhiteSpace(_outputBaseName)
                         && !_isApplying
                         && !_isConverting;

    public bool CanExportPack => Tracks.Any(t => t.HasFile) && !_isApplying && !_isConverting;

    private string? _lastOutputRomPath;
    public bool CanLaunch => _lastOutputRomPath is not null && File.Exists(_lastOutputRomPath);

    public string AssignedCountText
    {
        get
        {
            int count = Tracks.Count(t => t.HasFile);
            return count == 0 ? string.Empty : $"{count} of {Tracks.Count} tracks assigned";
        }
    }

    public string LibraryTargetSlotName => _libraryTargetSlot is null
        ? "Music Library"
        : $"Slot {_libraryTargetSlot.SlotNumber} — {_libraryTargetSlot.Name}";

    public string CurrentPlaylistDisplayName =>
        _currentPlaylistName is not null
            ? (_hasUnsavedPlaylistChanges ? $"{_currentPlaylistName} *" : _currentPlaylistName)
            : "(unsaved)";

    public bool IsMusicExpanded
    {
        get => _isMusicExpanded;
        set { _isMusicExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(MusicExpandIcon)); }
    }
    public bool IsConfigExpanded
    {
        get => _isConfigExpanded;
        set { _isConfigExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfigExpandIcon)); }
    }
    public bool IsAutoLauncherExpanded
    {
        get => _isAutoLauncherExpanded;
        set { _isAutoLauncherExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutoLauncherExpandIcon)); }
    }
    public string MusicExpandIcon        => _isMusicExpanded        ? "▼" : "▶";
    public string ConfigExpandIcon       => _isConfigExpanded       ? "▼" : "▶";
    public string AutoLauncherExpandIcon => _isAutoLauncherExpanded ? "▼" : "▶";

    // ── Library ───────────────────────────────────────────────────────────
    private readonly MusicLibrary _library = new();
    private TrackSlot? _libraryTargetSlot;
    private LibraryEntry? _libraryPlayingEntry;

    private static string DefaultLibraryFolder =>
        Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath!)!,
            "MusicLibrary");

    private static string DefaultOutputFolder =>
        Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath!)!,
            "Output");

    private string PlaylistsFolder =>
        _library.LibraryFolder is not null
            ? Path.Combine(_library.LibraryFolder, "Playlists")
            : Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "Playlists");

    public string? LibraryFolder => _library.LibraryFolder;
    public string LibrarySongCount => _library.Entries.Count == 0
        ? string.Empty
        : $"· {_library.Entries.Count} song{(_library.Entries.Count != 1 ? "s" : "")}";

    // ── Services ─────────────────────────────────────────────────────────
    private readonly AudioPlayer _audio = new();
    private TrackSlot? _playingSlot;
    private TrackSlot? _playingOriginalSlot;
    private readonly ApplyEngine _applyEngine = new();
    private CancellationTokenSource _closeCts = new();
    private static readonly System.Net.Http.HttpClient Http = Services.SharedHttp.Client;

    private static string GetSpritePreviewCachePath(string url)
    {
        // Use the same Previews cache dir as SpriteImageControl so images downloaded during
        // browsing are reused immediately without a second HTTP request.
        try
        {
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName))
                fileName = Math.Abs(url.GetHashCode()).ToString("x8") + ".png";
            fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LTTPEnhancementTools", "SpriteCache", "Previews", fileName);
        }
        catch
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LTTPEnhancementTools", "SpriteCache", "Previews",
                Convert.ToHexString(hash)[..16] + ".png");
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        LoadTrackCatalog();
        OriginalSoundtrackManager.LoadCachedOriginals(Tracks.ToList());

        _audio.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
        {
            if (_playingSlot is not null)
            {
                _playingSlot.IsPlaying = false;
                _playingSlot = null;
            }
            if (_playingOriginalSlot is not null)
            {
                _playingOriginalSlot.IsPlayingOriginal = false;
                _playingOriginalSlot = null;
            }
        });

        _applyEngine.ConflictsDetected += OnConflictsDetected;

        // Set up library and output folders (auto-create next to exe on first run)
        var settings = SettingsManager.Load();

        string libraryPath = string.IsNullOrEmpty(settings.LibraryFolder)
            ? DefaultLibraryFolder
            : settings.LibraryFolder;
        Directory.CreateDirectory(libraryPath);
        _library.SetFolder(libraryPath);
        Directory.CreateDirectory(Path.Combine(libraryPath, "Playlists"));

        string outputPath = string.IsNullOrEmpty(settings.OutputFolder)
            ? DefaultOutputFolder
            : settings.OutputFolder;
        Directory.CreateDirectory(outputPath);
        OutputDir = outputPath;

        // Load launch settings (null = file doesn't exist yet; wizard shown in Window_Loaded)
        var launchSettings = LaunchSettingsManager.TryLoad();
        if (launchSettings is not null)
        {
            _emulatorPath        = launchSettings.EmulatorPath.NullIfEmpty();
            _connectorScriptPath = launchSettings.ConnectorScriptPath.NullIfEmpty();
            _sniPath             = launchSettings.SniPath.NullIfEmpty();
            _archipelagoLauncherPath = launchSettings.ArchipelagoLauncherPath.NullIfEmpty();
            _trackerUrl          = launchSettings.TrackerUrl.NullIfEmpty();
            _seedUrl             = launchSettings.SeedUrl.NullIfEmpty();
        }

        // Persist any defaulted paths
        if (string.IsNullOrEmpty(settings.LibraryFolder) || string.IsNullOrEmpty(settings.OutputFolder))
            SaveAppSettings();

        // Auto-restore last sprite and last playlist (use backing fields directly to avoid
        // triggering SaveAutoState() or StopCurrentPlayback() before initialization is complete)
        var autoState = AutoSaveManager.Load();

        string? savedSprite = autoState.LastSpritePath.NullIfEmpty();
        bool isSentinel = savedSprite == SpriteBrowserWindow.RandomAllSentinel ||
                          savedSprite == SpriteBrowserWindow.RandomFavoritesSentinel;
        if (savedSprite is not null && (File.Exists(savedSprite) || isSentinel))
        {
            _spritePath = savedSprite;
            if (!isSentinel)
            {
                var savedPreview = autoState.LastSpritePreviewUrl.NullIfEmpty();
                if (savedPreview is not null && savedPreview.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Stale web URL saved before caching completed (e.g. app crashed) — re-cache it.
                    // Use Dispatcher so the constructor finishes before the async work starts.
                    Dispatcher.InvokeAsync(() => _ = CacheSpritePreviewAsync(savedPreview));
                }
                else
                {
                    _spritePreviewUrl = savedPreview;
                }
            }
        }

        string? savedPlaylist = autoState.LastPlaylistPath.NullIfEmpty();
        if (savedPlaylist is not null && File.Exists(savedPlaylist))
        {
            var (playlist, _) = PlaylistManager.Load(savedPlaylist);
            if (playlist is not null)
            {
                foreach (var (key, path) in playlist.Tracks)
                {
                    if (!int.TryParse(key, out int slot)) continue;
                    var ts = Tracks.FirstOrDefault(t => t.SlotNumber == slot);
                    if (ts is null) continue;
                    ts.PcmPath = path;
                    ts.ValidationError = PcmValidator.Validate(path);
                }
                _currentPlaylistPath = savedPlaylist;
                _currentPlaylistName = playlist.Name;
                _hasUnsavedPlaylistChanges = false;
            }
        }

        OnPropertyChanged(nameof(HasSprite));
        OnPropertyChanged(nameof(SpriteDisplayName));
        OnPropertyChanged(nameof(IsRandomSprite));
        OnPropertyChanged(nameof(RandomGlyph));
        OnPropertyChanged(nameof(SpritePreviewUrl));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(AssignedCountText));
        OnPropertyChanged(nameof(CurrentPlaylistDisplayName));
        OnPropertyChanged(nameof(LibraryFolder));
        OnPropertyChanged(nameof(LibrarySongCount));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SyncTrackerCombo();

        // Show setup wizard on first run — deferred so main window is fully rendered first
        if (!LaunchSettingsManager.FileExists())
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, RunSetupWizard);

        // Load the default Link Nintendo preview from the cached sprites list (if available)
        _ = LoadDefaultLinkPreviewAsync();
    }

    private async Task LoadDefaultLinkPreviewAsync()
    {
        try
        {
            // Prefer the URL from the loaded sprite list (handles future URL changes),
            // but fall back to the hardcoded S3 URL so this works on first run too.
            var previewUrl = SpriteBrowserWindow.DefaultLinkPreviewUrl
                          ?? SpriteBrowserWindow.DefaultLinkPreviewFallbackUrl;

            var cachePath = GetSpritePreviewCachePath(previewUrl);
            if (!File.Exists(cachePath))
            {
                var bytes = await Http.GetByteArrayAsync(previewUrl);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                await File.WriteAllBytesAsync(cachePath, bytes);
            }

            DefaultSpritePreviewUrl = cachePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] Default Link preview failed: {ex.Message}");
        }
    }

    private void SyncTrackerCombo()
    {
        // Unsubscribe while syncing to avoid triggering SaveLaunchSettings via SelectionChanged
        TrackerCombo.SelectionChanged -= TrackerCombo_SelectionChanged;
        foreach (ComboBoxItem item in TrackerCombo.Items)
        {
            if ((item.Tag as string) == (_trackerUrl ?? string.Empty))
            {
                TrackerCombo.SelectedItem = item;
                TrackerCombo.SelectionChanged += TrackerCombo_SelectionChanged;
                return;
            }
        }
        TrackerCombo.SelectedIndex = 0;
        TrackerCombo.SelectionChanged += TrackerCombo_SelectionChanged;
    }

    private void RunSetupWizard()
    {
        try
        {
            var existing = LaunchSettingsManager.TryLoad();
            var wizard = new SetupWizardWindow(existing) { Owner = this };

            if (wizard.ShowDialog() == true && wizard.Result is not null)
            {
                _emulatorPath        = wizard.Result.EmulatorPath.NullIfEmpty();
                _connectorScriptPath = wizard.Result.ConnectorScriptPath.NullIfEmpty();
                _sniPath             = wizard.Result.SniPath.NullIfEmpty();
                _archipelagoLauncherPath = wizard.Result.ArchipelagoLauncherPath.NullIfEmpty();
                _trackerUrl          = wizard.Result.TrackerUrl.NullIfEmpty();
                OnPropertyChanged(nameof(EmulatorPath));
                OnPropertyChanged(nameof(ConnectorScriptPath));
                OnPropertyChanged(nameof(SniPath));
                OnPropertyChanged(nameof(ArchipelagoLauncherPath));
                OnPropertyChanged(nameof(TrackerUrl));
                SyncTrackerCombo();
                AppendLog("Launch settings saved.");
            }
            else if (!LaunchSettingsManager.FileExists())
            {
                // User skipped — write empty file so wizard doesn't reappear
                LaunchSettingsManager.Save(new LaunchSettings());
                AppendLog("Setup skipped. Use '⚙ Run Setup Wizard…' in Auto Launcher to configure later.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Setup wizard failed: {ex.Message}");
        }
    }

    private void SetupWizard_Click(object sender, RoutedEventArgs e) => RunSetupWizard();

    // ── Section expanders ─────────────────────────────────────────────────
    private void ToggleMusic_Click(object s, RoutedEventArgs e)        => IsMusicExpanded        = !IsMusicExpanded;
    private void ToggleConfig_Click(object s, RoutedEventArgs e)       => IsConfigExpanded       = !IsConfigExpanded;
    private void ToggleAutoLauncher_Click(object s, RoutedEventArgs e) => IsAutoLauncherExpanded = !IsAutoLauncherExpanded;

    // ── Track Catalog ─────────────────────────────────────────────────────
    private void LoadTrackCatalog()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/trackCatalog.json");
            using var stream = Application.GetResourceStream(uri)!.Stream;
            var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(stream, Services.JsonDefaults.ReadOnly);

            if (entries is null) return;

            foreach (var e in entries.OrderBy(e => e.Slot))
            {
                Tracks.Add(new TrackSlot { SlotNumber = e.Slot, Name = e.Name, TrackType = e.Type });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Failed to load track catalog: {ex.Message}");
        }
    }

    private record CatalogEntry(int Slot, string Name, string Type = "music");

    // ── ROM Selection ─────────────────────────────────────────────────────
    private void SelectRom_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select ALttP Randomizer ROM",
            Filter = "SNES ROM Files (*.sfc;*.smc;*.snes)|*.sfc;*.smc;*.snes|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == true)
        {
            long fileSize = new FileInfo(dlg.FileName).Length;
            long effectiveSize = (fileSize % 1024 == 512) ? fileSize - 512 : fileSize;

            if (effectiveSize < 2_097_152)
            {
                MessageBox.Show(
                    "This appears to be a vanilla (unpatched) ROM.\n\n" +
                    "MSU-1 music packs require a randomized ROM. " +
                    "Please generate your randomized ROM first, then select it here.",
                    "Vanilla ROM Detected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _romPath = dlg.FileName;
            RomBaseName = Path.GetFileNameWithoutExtension(dlg.FileName);
            OutputBaseName = RomBaseName;
            AppendLog($"ROM selected: {_romPath}");
        }
    }

    // ── PCM Replace / Pick ────────────────────────────────────────────────
    private async void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is TrackSlot slot)
            await SafeAsync(() => PickPcmForSlot(slot));
    }

    private async Task PickPcmForSlot(TrackSlot slot)
    {
        var dlg = new OpenFileDialog
        {
            Title = $"Select audio for Slot {slot.SlotNumber} — {slot.Name}",
            Filter = "MSU-1 PCM & Audio Files (*.pcm;*.mp3;*.wav;*.wma;*.wmv;*.aac;*.m4a;*.mp4;*.aiff;*.aif)" +
                     "|*.pcm;*.mp3;*.wav;*.wma;*.wmv;*.aac;*.m4a;*.mp4;*.aiff;*.aif" +
                     "|MSU-1 PCM (*.pcm)|*.pcm" +
                     "|MP3 (*.mp3)|*.mp3" +
                     "|WAV (*.wav)|*.wav" +
                     "|WMA / WMV (*.wma;*.wmv)|*.wma;*.wmv" +
                     "|AAC / M4A / MP4 (*.aac;*.m4a;*.mp4)|*.aac;*.m4a;*.mp4" +
                     "|AIFF (*.aiff;*.aif)|*.aiff;*.aif" +
                     "|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != true) return;

        string path = dlg.FileName;

        // Offer to copy to library if library is configured and file isn't already inside it
        if (_library.LibraryFolder is not null)
        {
            string destFileName = Path.GetFileName(path);
            string destPath     = Path.Combine(_library.LibraryFolder, destFileName);
            bool alreadyInLibrary = path.Equals(destPath, StringComparison.OrdinalIgnoreCase)
                                 || path.StartsWith(_library.LibraryFolder + Path.DirectorySeparatorChar,
                                                    StringComparison.OrdinalIgnoreCase);

            if (!alreadyInLibrary)
            {
                var copyResult = MessageBox.Show(
                    $"Copy \"{destFileName}\" to your Music Library?\n\n" +
                    "This lets you reuse it from the library picker on other slots.",
                    "Add to Music Library?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (copyResult == MessageBoxResult.Yes)
                {
                    if (!File.Exists(destPath))
                    {
                        File.Copy(path, destPath);
                        _library.Refresh();
                        OnPropertyChanged(nameof(LibrarySongCount));
                        AppendLog($"Copied to library: {destFileName}");
                    }
                    else
                    {
                        AppendLog($"File already in library: {destFileName}");
                    }
                    path = destPath; // use library copy as source going forward
                }
            }
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".pcm")
        {
            ValidateAndAssignPcm(path, slot, Path.GetFileName(path));
        }
        else
        {
            string destPath = Path.ChangeExtension(path, ".pcm");
            await ConvertAndAssignPcmAsync(path, destPath, slot);
        }
    }

    // ── Shared PCM assignment helpers ──────────────────────────────────────
    private void ValidateAndAssignPcm(string pcmPath, TrackSlot slot, string displayName)
    {
        string? error = PcmValidator.Validate(pcmPath);
        slot.PcmPath = pcmPath;
        slot.ValidationError = error;

        if (error is not null)
            AppendLog($"[WARN] Slot {slot.SlotNumber}: {error} (file: {displayName})");
        else
            AppendLog($"Slot {slot.SlotNumber} assigned: {displayName}");

        SetDirty();
        OnPropertyChanged(nameof(AssignedCountText));
        OnPropertyChanged(nameof(CanApply));
    }

    private async Task<bool> ConvertAndAssignPcmAsync(string sourcePath, string destPath, TrackSlot slot)
    {
        string srcName = Path.GetFileName(sourcePath);
        string destName = Path.GetFileName(destPath);
        AppendLog($"[Slot {slot.SlotNumber}] Converting {srcName} → {destName}...");

        _isConverting = true;
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanExportPack));
        try
        {
            string? error = await PcmConverter.ConvertAsync(sourcePath, destPath);
            if (error is not null)
            {
                AppendLog($"[ERROR] Slot {slot.SlotNumber}: {error}");
                return false;
            }

            AppendLog($"[Slot {slot.SlotNumber}] Conversion complete → {destName}");
            ValidateAndAssignPcm(destPath, slot, destName);
            return true;
        }
        finally
        {
            _isConverting = false;
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanExportPack));
        }
    }

    // ── Playback helpers ──────────────────────────────────────────────────
    /// <summary>Stops the current slot preview and clears playback state.</summary>
    private void StopCurrentPlayback()
    {
        if (_playingSlot is not null)
        {
            _audio.Stop();
            _playingSlot.IsPlaying = false;
            _playingSlot = null;
        }
        StopOriginalPlayback();
    }

    private void StopOriginalPlayback()
    {
        if (_playingOriginalSlot is not null)
        {
            _audio.Stop();
            _playingOriginalSlot.IsPlayingOriginal = false;
            _playingOriginalSlot = null;
        }
    }

    // ── Clear ─────────────────────────────────────────────────────────────
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is TrackSlot slot)
        {
            if (_playingSlot == slot)
                StopCurrentPlayback();
            slot.PcmPath = null;
            slot.ValidationError = null;
            SetDirty();
            OnPropertyChanged(nameof(AssignedCountText));
            OnPropertyChanged(nameof(CanApply));
        }
    }

    // ── Audio Preview ─────────────────────────────────────────────────────
    private void PlayStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is TrackSlot slot)
        {
            if (slot.IsPlaying)
            {
                _audio.Stop();
                slot.IsPlaying = false;
                _playingSlot = null;
            }
            else if (slot.PcmPath is not null)
            {
                StopCurrentPlayback();

                string? error = _audio.Play(slot.PcmPath);
                if (error is not null)
                {
                    AppendLog($"[ERROR] Playback failed for slot {slot.SlotNumber}: {error}");
                    MessageBox.Show(error, "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    slot.IsPlaying = true;
                    _playingSlot = slot;
                }
            }
        }
    }

    // ── Original Audio Preview ───────────────────────────────────────────
    private void PlayOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is TrackSlot slot)
        {
            if (slot.IsPlayingOriginal)
            {
                _audio.Stop();
                slot.IsPlayingOriginal = false;
                _playingOriginalSlot = null;
            }
            else if (slot.OriginalPcmPath is not null)
            {
                StopCurrentPlayback();

                string? error = _audio.Play(slot.OriginalPcmPath);
                if (error is not null)
                {
                    AppendLog($"[ERROR] Original playback failed for slot {slot.SlotNumber}: {error}");
                    MessageBox.Show(error, "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    slot.IsPlayingOriginal = true;
                    _playingOriginalSlot = slot;
                }
            }
        }
    }

    // ── Import Original Soundtrack ───────────────────────────────────────
    private void ImportOriginals_Click(object sender, RoutedEventArgs e)
    {
        // Show a context menu with import options
        var menu = new System.Windows.Controls.ContextMenu();

        var folderItem = new System.Windows.Controls.MenuItem { Header = "Import from Folder…" };
        folderItem.Click += async (_, _) => await SafeAsync(ImportOriginalsFromFolder);
        menu.Items.Add(folderItem);

        var zipItem = new System.Windows.Controls.MenuItem { Header = "Import from ZIP…" };
        zipItem.Click += async (_, _) => await SafeAsync(ImportOriginalsFromZip);
        menu.Items.Add(zipItem);

        var urlItem = new System.Windows.Controls.MenuItem { Header = "Import from URL…" };
        urlItem.Click += async (_, _) => await SafeAsync(ImportOriginalsFromUrl);
        menu.Items.Add(urlItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exportItem = new System.Windows.Controls.MenuItem
        {
            Header = "Export Originals as Pack…",
            IsEnabled = Tracks.Any(t => t.HasOriginal)
        };
        exportItem.Click += async (_, _) => await SafeAsync(ExportOriginalsAsPack);
        menu.Items.Add(exportItem);

        var clearItem = new System.Windows.Controls.MenuItem
        {
            Header = "Clear All Originals",
            IsEnabled = Tracks.Any(t => t.HasOriginal)
        };
        clearItem.Click += (_, _) =>
        {
            OriginalSoundtrackManager.ClearCache(Tracks.ToList());
            AppendLog("[INFO] Cleared all cached original soundtrack files.");
        };
        menu.Items.Add(clearItem);

        if (sender is Button button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
        }
        menu.IsOpen = true;
    }

    private async Task ImportOriginalsFromFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder containing original ALttP soundtrack MP3 files"
        };

        if (dlg.ShowDialog(this) != true) return;

        AppendLog("Importing original soundtrack from folder…");
        string? error = await OriginalSoundtrackManager.ImportFromFolderAsync(
            dlg.FolderName, Tracks.ToList(), MakeImportProgress());
        HandleImportResult(error);
    }

    private async Task ImportOriginalsFromZip()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select ZIP containing original ALttP soundtrack",
            Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != true) return;

        AppendLog("Importing original soundtrack from ZIP…");
        string? error = await OriginalSoundtrackManager.ImportFromZipAsync(
            dlg.FileName, Tracks.ToList(), MakeImportProgress());
        HandleImportResult(error);
    }

    private async Task ImportOriginalsFromUrl()
    {
        var inputDialog = new Window
        {
            Title = "Import from URL",
            Width = 500, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = FindResource("BgBrush") as Brush,
            Foreground = FindResource("TextBrush") as Brush
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Enter URL to a ZIP file containing original soundtrack MP3s:",
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = FindResource("TextBrush") as Brush
        });
        var urlBox = new System.Windows.Controls.TextBox
        {
            Style = FindResource("DarkTextBox") as Style,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(urlBox);
        var btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var okBtn = new Button { Content = "Download & Import", Style = FindResource("AccentButton") as Style, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var cancelBtn = new Button { Content = "Cancel", Style = FindResource("DarkButton") as Style, Padding = new Thickness(12, 6, 12, 6) };
        okBtn.Click += (_, _) => { inputDialog.DialogResult = true; inputDialog.Close(); };
        cancelBtn.Click += (_, _) => { inputDialog.DialogResult = false; inputDialog.Close(); };
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        stack.Children.Add(btnPanel);
        inputDialog.Content = stack;

        if (inputDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(urlBox.Text)) return;

        AppendLog($"Downloading original soundtrack from URL…");
        string? error = await OriginalSoundtrackManager.ImportFromUrlAsync(
            urlBox.Text.Trim(), Tracks.ToList(), Http, MakeImportProgress());
        HandleImportResult(error);
    }

    private Progress<(int current, int total, string trackName)> MakeImportProgress()
        => new(p => AppendLog($"  Converting {p.current}/{p.total}: {p.trackName}"));

    private void HandleImportResult(string? error)
    {
        if (error is not null)
        {
            AppendLog($"[WARN] {error}");
            MessageBox.Show(error, "Import Original Soundtrack", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            int count = Tracks.Count(t => t.HasOriginal);
            AppendLog($"Original soundtrack imported: {count} track(s) available for preview.");
        }
    }

    private async Task ExportOriginalsAsPack()
    {
        var playlist = new Playlist
        {
            Name = "Vanilla ALttP Soundtrack",
            Tracks = Tracks
                .Where(t => t.OriginalPcmPath is not null)
                .ToDictionary(t => t.SlotNumber.ToString(), t => t.OriginalPcmPath!)
        };

        var dlg = new SaveFileDialog
        {
            Title = "Export Original Soundtrack as Pack",
            Filter = "LTTP Pack (*.lttppack)|*.lttppack|All Files (*.*)|*.*",
            DefaultExt = ".lttppack",
            FileName = "Vanilla ALttP Soundtrack"
        };

        if (dlg.ShowDialog(this) != true) return;

        var (result, error) = await Task.Run(() => PlaylistBundleManager.Export(dlg.FileName, playlist));

        if (error is not null)
        {
            AppendLog($"[ERROR] Export failed: {error}");
            MessageBox.Show(error, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            AppendLog($"Original soundtrack exported: {result!.TracksWritten} track(s) → {dlg.FileName}");
        }
    }

    // ── Playlist Save / Load ──────────────────────────────────────────────
    private void SavePlaylist_Click(object sender, RoutedEventArgs e) => SavePlaylistDialog();

    private async void ExportPack_Click(object sender, RoutedEventArgs e)
    {
        var playlist = BuildPlaylist(_currentPlaylistName ?? "my-pack");

        string initialDir = Directory.Exists(PlaylistsFolder) ? PlaylistsFolder
            : Path.GetDirectoryName(Environment.ProcessPath!)!;

        var dlg = new SaveFileDialog
        {
            Title = "Export LTTP Pack",
            Filter = "LTTP Pack (*.lttppack)|*.lttppack|All Files (*.*)|*.*",
            DefaultExt = ".lttppack",
            FileName = playlist.Name,
            InitialDirectory = initialDir
        };

        if (dlg.ShowDialog(this) != true) return;

        var (result, error) = await Task.Run(() => PlaylistBundleManager.Export(dlg.FileName, playlist));

        if (error is not null)
        {
            AppendLog($"[ERROR] Export failed: {error}");
            MessageBox.Show(error, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (result!.TracksSkipped > 0)
        {
            string msg = $"{result.TracksSkipped} track file(s) were missing and could not be bundled.\n" +
                         $"{result.TracksWritten} track(s) were exported successfully.";
            AppendLog($"[WARN] Export partial: {msg}");
            MessageBox.Show(msg, "Export Complete (with warnings)", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            AppendLog($"Pack exported: {result.TracksWritten} track(s) → {dlg.FileName}");
        }
    }

    private async void ImportPack_Click(object sender, RoutedEventArgs e)
    {
        if (_library.LibraryFolder is null)
        {
            MessageBox.Show(
                "Please configure a music library folder before importing a pack.",
                "No Library Configured",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string initialDir = Directory.Exists(PlaylistsFolder) ? PlaylistsFolder
            : Path.GetDirectoryName(Environment.ProcessPath!)!;

        var dlg = new OpenFileDialog
        {
            Title = "Import LTTP Pack",
            Filter = "LTTP Pack (*.lttppack)|*.lttppack|All Files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = initialDir
        };

        if (dlg.ShowDialog(this) != true) return;

        var (playlist, error) = await Task.Run(
            () => PlaylistBundleManager.Import(dlg.FileName, _library.LibraryFolder));

        if (error is not null)
        {
            AppendLog($"[ERROR] Import failed: {error}");
            MessageBox.Show(error, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Pass null as sourceFilePath — leaves the session as "unsaved" so the .lttppack
        // path is never written to auto-save (PlaylistManager.Load would fail on it later).
        ApplyPlaylist(playlist!, null);
        _library.Refresh();
        OnPropertyChanged(nameof(LibrarySongCount));
        SaveAutoState();
        AppendLog($"Pack imported: '{playlist!.Name}' — {playlist.Tracks.Count} track(s) loaded.");
    }

    private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
    {
        string initialDir = Directory.Exists(PlaylistsFolder) ? PlaylistsFolder
            : Path.GetDirectoryName(Environment.ProcessPath!)!;

        var dlg = new OpenFileDialog
        {
            Title = "Load Playlist",
            Filter = "JSON Playlist (*.json)|*.json|All Files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = initialDir
        };

        if (dlg.ShowDialog(this) != true) return;

        var (playlist, error) = PlaylistManager.Load(dlg.FileName);
        if (error is not null)
        {
            AppendLog($"[ERROR] {error}");
            return;
        }

        ApplyPlaylist(playlist!, dlg.FileName);
        SaveAutoState();
        AppendLog($"Playlist loaded: {dlg.FileName} ({playlist!.Tracks.Count} track(s))");
    }

    private bool SavePlaylistDialog()
    {
        Directory.CreateDirectory(PlaylistsFolder);

        var dlg = new SaveFileDialog
        {
            Title = "Save Playlist",
            Filter = "JSON Playlist (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = _currentPlaylistName ?? "my-playlist",
            InitialDirectory = PlaylistsFolder
        };

        if (dlg.ShowDialog(this) != true) return false;

        string playlistName = Path.GetFileNameWithoutExtension(dlg.FileName);
        var playlist = BuildPlaylist(playlistName);
        string? error = PlaylistManager.Save(dlg.FileName, playlist);

        if (error is not null)
        {
            AppendLog($"[ERROR] {error}");
            return false;
        }

        _currentPlaylistPath = dlg.FileName;
        _currentPlaylistName = playlistName;
        _hasUnsavedPlaylistChanges = false;
        OnPropertyChanged(nameof(CurrentPlaylistDisplayName));
        SaveAutoState();
        AppendLog($"Playlist saved: {dlg.FileName}");
        return true;
    }

    private Playlist BuildPlaylist(string name) => new Playlist
    {
        Name   = name,
        Tracks = Tracks.Where(t => t.PcmPath is not null)
                       .ToDictionary(t => t.SlotNumber.ToString(), t => t.PcmPath!)
    };

    private void ApplyPlaylist(Playlist playlist, string? sourceFilePath)
    {
        StopCurrentPlayback();
        foreach (var t in Tracks) { t.PcmPath = null; t.ValidationError = null; }
        foreach (var (key, path) in playlist.Tracks)
        {
            if (!int.TryParse(key, out int slot)) continue;
            var ts = Tracks.FirstOrDefault(t => t.SlotNumber == slot);
            if (ts is null) continue;
            ts.PcmPath = path;
            ts.ValidationError = PcmValidator.Validate(path);
        }
        _currentPlaylistPath = sourceFilePath;
        _currentPlaylistName = playlist.Name;
        _hasUnsavedPlaylistChanges = false;
        OnPropertyChanged(nameof(CurrentPlaylistDisplayName));
        OnPropertyChanged(nameof(AssignedCountText));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanExportPack));
    }

    // ── Dirty tracking ────────────────────────────────────────────────────
    private void SetDirty()
    {
        _hasUnsavedPlaylistChanges = true;
        OnPropertyChanged(nameof(CurrentPlaylistDisplayName));
        OnPropertyChanged(nameof(CanExportPack));
    }

    // ── Auto-save ─────────────────────────────────────────────────────────
    private void SaveAutoState() =>
        AutoSaveManager.Save(new AutoSaveState
        {
            LastSpritePath       = _spritePath        ?? string.Empty,
            LastSpritePreviewUrl = _spritePreviewUrl   ?? string.Empty,
            LastPlaylistPath     = _currentPlaylistPath ?? string.Empty,
        });

    // ── Output Dir Browse ─────────────────────────────────────────────────
    private void BrowseOutputDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Output Folder for MSU Pack"
        };

        if (dlg.ShowDialog(this) == true)
        {
            Directory.CreateDirectory(dlg.FolderName);
            OutputDir = dlg.FolderName;
            SaveAppSettings();
            AppendLog($"Output folder: {OutputDir}");
        }
    }

    private async void CleanOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (OutputDir is null || !Directory.Exists(OutputDir)) return;

        var files = Directory.GetFiles(OutputDir);
        if (files.Length == 0)
        {
            AppendLog("Output folder is already empty.");
            return;
        }

        var result = MessageBox.Show(
            $"Delete all {files.Length} file(s) in:\n\n{OutputDir}\n\nThis cannot be undone.",
            "Clear Output Folder?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        int deleted = await Task.Run(() =>
        {
            int count = 0;
            foreach (var f in files)
            {
                try { File.Delete(f); count++; }
                catch { /* skip locked files */ }
            }
            return count;
        });
        AppendLog($"Cleared {deleted} file(s) from output folder.");
    }

    // ── Apply ─────────────────────────────────────────────────────────────
    private async Task<bool> ApplyCoreAsync()
    {
        if (_romPath is null || _outputDir is null) return false;

        // Prompt to save playlist if tracks are assigned but no playlist has been saved yet
        if (Tracks.Any(t => t.HasFile) && _hasUnsavedPlaylistChanges && _currentPlaylistPath is null)
        {
            var answer = MessageBox.Show(
                "Save your track assignments as a playlist before applying?",
                "Save Playlist?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
                SavePlaylistDialog(); // user can cancel the dialog; apply proceeds regardless
        }

        StopCurrentPlayback();

        _isApplying = true;
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanExportPack));
        ApplySuccessText.Visibility = Visibility.Collapsed;
        ProgressSection.Visibility = Visibility.Visible;
        ApplyProgress.Value = 0;
        ProgressStepText.Text = string.Empty;

        var tracks = Tracks
            .Where(t => t.HasFile)
            .ToDictionary(t => t.SlotNumber.ToString(), t => t.PcmPath!);

        // Resolve random sentinel to a concrete sprite path before applying
        string? resolvedSpritePath = _spritePath;
        if (IsRandomSprite)
        {
            bool favsOnly = _spritePath == SpriteBrowserWindow.RandomFavoritesSentinel;
            resolvedSpritePath = await PickRandomSpriteAsync(favsOnly);
            if (resolvedSpritePath is null)
            {
                // PickRandomSpriteAsync already logged the error
                _isApplying = false;
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanExportPack));
                return false;
            }
        }

        var req = new ApplyRequest(_romPath, _outputDir, tracks, OverwriteMode.Ask, OutputBaseName?.Trim(), resolvedSpritePath);

        var progress = new Progress<(string step, int current, int total)>(p =>
        {
            ApplyProgress.Value = p.total > 0 ? (double)p.current / p.total * 100 : 0;
            ProgressStepText.Text = p.step;
        });

        try
        {
            var result = await _applyEngine.RunAsync(req, progress, _closeCts.Token);

            ProgressSection.Visibility = Visibility.Collapsed;
            ApplyProgress.Value = 100;
            ApplySuccessText.Text = $"Done! {result.FilesWritten.Count} file(s) written.";
            ApplySuccessText.Visibility = Visibility.Visible;

            // Track the output ROM for the Launch button
            string romExt = Path.GetExtension(_romPath!);
            string baseName = !string.IsNullOrWhiteSpace(_outputBaseName) ? _outputBaseName.Trim() : Path.GetFileNameWithoutExtension(_romPath!);
            _lastOutputRomPath = Path.Combine(_outputDir!, baseName + romExt);
            OnPropertyChanged(nameof(CanLaunch));

            AppendLog($"Apply succeeded. {result.FilesWritten.Count} file(s) written to: {_outputDir}");
            foreach (var f in result.FilesWritten)
                AppendLog($"  + {Path.GetFileName(f)}");

            return true;
        }
        catch (OperationCanceledException)
        {
            ProgressSection.Visibility = Visibility.Collapsed;
            AppendLog("Apply cancelled.");
            return false;
        }
        catch (FileNotFoundException fnf)
        {
            ProgressSection.Visibility = Visibility.Collapsed;
            AppendLog($"[ERROR] {fnf.Message}");
            MessageBox.Show(fnf.Message, "Apply Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        catch (Exception ex)
        {
            ProgressSection.Visibility = Visibility.Collapsed;
            AppendLog($"[ERROR] Apply failed: {ex.Message}");
            MessageBox.Show(ex.Message, "Apply Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _isApplying = false;
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanExportPack));
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e) => await ApplyCoreAsync();

    private async void AutoLaunch_Click(object sender, RoutedEventArgs e)
    {
        bool hasTracker = !string.IsNullOrEmpty(_trackerUrl);
        bool hasArchipelago = !string.IsNullOrEmpty(_sniPath) || !string.IsNullOrEmpty(_archipelagoLauncherPath);

        var dialog = new AutoLaunchDialog(hasTracker, hasArchipelago) { Owner = this };
        if (dialog.ShowDialog() == true)
            await LaunchRomCoreAsync(dialog.SelectedOption);
    }

    // ── Conflict Modal ────────────────────────────────────────────────────
    private void OnConflictsDetected(object? sender, ConflictsDetectedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var names = string.Join("\n", e.Conflicts.Select(c => $"  • {c.FileName}"));
            string msg = $"The output folder already has music files:\n\n{names}\n\n"
                       + "Yes = Overwrite with new tracks\n"
                       + "No = Keep existing tracks (skip)\n"
                       + "Cancel = Abort apply";

            var result = MessageBox.Show(msg, "Existing Music Files",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Cancel,
                MessageBoxOptions.None);

            // Yes = Overwrite, No = Skip, Cancel = cancel
            e.Resolution = result switch
            {
                MessageBoxResult.Yes => OverwriteMode.Overwrite,
                MessageBoxResult.No  => OverwriteMode.Skip,
                _                    => OverwriteMode.Ask  // treated as cancel in engine
            };
            e.Complete();
        });
    }

    // ── Music Library ─────────────────────────────────────────────────────
    private void BrowseLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music Library Folder"
        };

        if (dlg.ShowDialog(this) != true) return;

        Directory.CreateDirectory(dlg.FolderName);
        _library.SetFolder(dlg.FolderName);
        Directory.CreateDirectory(Path.Combine(dlg.FolderName, "Playlists"));
        SaveAppSettings();
        OnPropertyChanged(nameof(LibraryFolder));
        OnPropertyChanged(nameof(LibrarySongCount));
        AppendLog($"Music library: {dlg.FolderName} ({_library.Entries.Count} song(s) found)");
    }

    private void LibraryPick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ButtonBase btn || btn.CommandParameter is not TrackSlot slot) return;

        if (_library.LibraryFolder is null)
        {
            AppendLog("Music library not configured.");
            return;
        }

        // Refresh to pick up any files added since last open
        _library.Refresh();
        OnPropertyChanged(nameof(LibrarySongCount));

        if (_library.Entries.Count == 0)
        {
            AppendLog($"Music library is empty: {_library.LibraryFolder}");
            return;
        }

        _libraryTargetSlot = slot;
        OnPropertyChanged(nameof(LibraryTargetSlotName));
        LibrarySearch.Text = string.Empty;
        LibraryResultCount.Text = string.Empty;
        LibraryList.ItemsSource = _library.Entries;
        LibraryPopup.PlacementTarget = btn;
        LibraryPopup.Placement = PlacementMode.Bottom;
        LibraryPopup.IsOpen = true;
        LibrarySearch.Focus();
    }

    private void LibrarySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = LibrarySearch.Text.Trim();
        if (string.IsNullOrEmpty(q))
        {
            LibraryList.ItemsSource = _library.Entries;
            LibraryResultCount.Text = string.Empty;
        }
        else
        {
            var filtered = _library.Entries.Where(en => en.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            LibraryList.ItemsSource = filtered;
            LibraryResultCount.Text = $"{filtered.Count}/{_library.Entries.Count}";
        }
    }

    private void LibraryItemPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not LibraryEntry entry) return;

        StopCurrentPlayback();

        _libraryPlayingEntry = entry;
        string? err = _audio.Play(entry.AssignablePath);
        if (err is not null)
            AppendLog($"[ERROR] Preview failed: {err}");
        else
            AppendLog($"Previewing: {entry.Name}");
    }

    private void LibraryItemAssign_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not LibraryEntry entry) return;
        _ = AssignLibraryEntryAsync(entry, _libraryTargetSlot!);
    }

    private void LibraryPopup_Closed(object? sender, EventArgs e)
    {
        if (_libraryPlayingEntry is not null)
        {
            _audio.Stop();
            _libraryPlayingEntry = null;
        }
    }

    private async Task AssignLibraryEntryAsync(LibraryEntry entry, TrackSlot slot)
    {
        LibraryPopup.IsOpen = false;

        if (!entry.NeedsConversion)
        {
            ValidateAndAssignPcm(entry.AssignablePath, slot, entry.Name);
        }
        else
        {
            if (_library.LibraryFolder is null) return;
            string cacheDir = Path.Combine(_library.LibraryFolder, "_cache");
            Directory.CreateDirectory(cacheDir);
            string destPath = _library.GetCacheTargetPath(entry.SourcePath);

            bool ok = await ConvertAndAssignPcmAsync(entry.SourcePath, destPath, slot);
            if (ok)
            {
                _library.Refresh();
                OnPropertyChanged(nameof(LibrarySongCount));
            }
        }
    }

    // ── Settings ──────────────────────────────────────────────────────────
    private void SaveAppSettings() =>
        SettingsManager.Save(new AppSettings
        {
            LibraryFolder = _library.LibraryFolder ?? string.Empty,
            OutputFolder  = _outputDir ?? string.Empty,
        });

    private void SaveLaunchSettings() =>
        LaunchSettingsManager.Save(new LaunchSettings
        {
            EmulatorPath        = _emulatorPath        ?? string.Empty,
            ConnectorScriptPath = _connectorScriptPath ?? string.Empty,
            SniPath             = _sniPath             ?? string.Empty,
            ArchipelagoLauncherPath = _archipelagoLauncherPath ?? string.Empty,
            TrackerUrl          = _trackerUrl          ?? string.Empty,
            SeedUrl             = _seedUrl             ?? string.Empty,
        });

    // ── Sprite Handlers ───────────────────────────────────────────────────
    private void BrowseSprite_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Link Sprite File",
            Filter = "Sprite Files (*.zspr;*.spr)|*.zspr;*.spr|ZSPR Sprite (*.zspr)|*.zspr|Legacy Sprite (*.spr)|*.spr|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != true) return;

        string? error = SpriteApplier.Validate(dlg.FileName);
        if (error is not null)
        {
            AppendLog($"[ERROR] Invalid sprite file: {error}");
            MessageBox.Show(error, "Invalid Sprite", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SpritePath = dlg.FileName;
        SpritePreviewUrl = null;
        AppendLog($"Sprite selected: {Path.GetFileName(dlg.FileName)}");
    }

    private void BrowseSpritesOnline_Click(object sender, RoutedEventArgs e)
    {
        var window = new SpriteBrowserWindow { Owner = this };
        if (window.ShowDialog() != true) return;

        // "Link" selected — reset to default (no custom sprite)
        if (window.SelectedIsDefault)
        {
            ClearSprite();
            return;
        }

        if (window.SelectedSpritePath is null) return;

        // Random sentinels — store as-is, resolve to real sprite at apply time
        if (window.SelectedSpritePath == SpriteBrowserWindow.RandomAllSentinel ||
            window.SelectedSpritePath == SpriteBrowserWindow.RandomFavoritesSentinel)
        {
            SpritePath = window.SelectedSpritePath;
            SpritePreviewUrl = null;
            AppendLog($"Sprite: {SpriteDisplayName} (resolved at apply time)");
            return;
        }

        string? error = SpriteApplier.Validate(window.SelectedSpritePath);
        if (error is not null)
        {
            AppendLog($"[ERROR] Invalid downloaded sprite: {error}");
            return;
        }

        SpritePath = window.SelectedSpritePath;

        // Don't set web URL directly — the converter can't load HTTPS in .NET 8.
        // CacheSpritePreviewAsync finds the already-downloaded preview (from SpriteImageControl's
        // cache) or downloads it, then sets SpritePreviewUrl to the local path.
        if (window.SelectedSpritePreviewUrl is not null)
            _ = CacheSpritePreviewAsync(window.SelectedSpritePreviewUrl);

        AppendLog($"Sprite selected: {Path.GetFileNameWithoutExtension(window.SelectedSpritePath)}");
    }

    private async Task CacheSpritePreviewAsync(string url)
    {
        try
        {
            string cachePath = GetSpritePreviewCachePath(url);
            if (!File.Exists(cachePath))
            {
                var bytes = await Http.GetByteArrayAsync(url);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                await File.WriteAllBytesAsync(cachePath, bytes);
            }
            // Switch to local cached file — unique per URL so binding always updates
            SpritePreviewUrl = cachePath;
            SaveAutoState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] Sprite preview cache failed: {ex.Message}");
        }
    }

    // ── Random sprite resolution ──────────────────────────────────────────

    private static readonly System.Text.Json.JsonSerializerOptions _randomJsonOpts = Services.JsonDefaults.ReadOnly;

    /// <summary>
    /// Picks a random sprite from the cached list (or fetches from network),
    /// downloads the ZSPR to the sprite cache if needed.
    /// Returns the local ZSPR path on success, or null (after logging) on failure.
    /// </summary>
    private async Task<string?> PickRandomSpriteAsync(bool favoritesOnly)
    {
        try
        {
            List<Models.SpriteEntry>? sprites;
            if (File.Exists(SpriteBrowserWindow.SpritesListCachePath))
            {
                var json = await File.ReadAllTextAsync(SpriteBrowserWindow.SpritesListCachePath);
                sprites = System.Text.Json.JsonSerializer.Deserialize<List<Models.SpriteEntry>>(json, _randomJsonOpts);
            }
            else
            {
                AppendLog("Fetching sprite list for random selection…");
                var json = await Http.GetStringAsync("https://alttpr.com/sprites");
                sprites = System.Text.Json.JsonSerializer.Deserialize<List<Models.SpriteEntry>>(json, _randomJsonOpts);
            }

            if (sprites is null || sprites.Count == 0)
            {
                AppendLog("[ERROR] No sprites available for random selection.");
                return null;
            }

            List<Models.SpriteEntry> pool = sprites;
            if (favoritesOnly)
            {
                var favs = Services.FavoritesManager.Load();
                pool = sprites.Where(s => favs.Contains(s.Name)).ToList();
                if (pool.Count == 0)
                {
                    AppendLog("[ERROR] No favorites found. Add favorites in the sprite browser first.");
                    return null;
                }
            }

            var picked = pool[Random.Shared.Next(pool.Count)];

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LTTPEnhancementTools", "SpriteCache");
            Directory.CreateDirectory(cacheDir);

            var safeName = string.Concat(picked.Name.Split(Path.GetInvalidFileNameChars()));
            var localPath = Path.Combine(cacheDir, safeName + ".zspr");

            if (!File.Exists(localPath))
            {
                AppendLog($"Downloading random sprite: {picked.Name}…");
                var data = await Http.GetByteArrayAsync(picked.File);
                await File.WriteAllBytesAsync(localPath, data);
            }

            return localPath;
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Random sprite failed: {ex.Message}");
            return null;
        }
    }

    private void ClearSprite_Click(object sender, RoutedEventArgs e) => ClearSprite();

    private void ClearSprite()
    {
        SpritePath = null;
        SpritePreviewUrl = null;
        AppendLog("Sprite cleared.");
    }

    // ── Track Search / Clear All ──────────────────────────────────────────
    private void TrackSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(Tracks);
        var q = TrackSearch.Text.Trim();
        view.Filter = string.IsNullOrEmpty(q)
            ? null
            : (obj => obj is TrackSlot s && s.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearAllTracks_Click(object sender, RoutedEventArgs e)
    {
        if (!Tracks.Any(t => t.HasFile)) return;

        var result = MessageBox.Show(
            "Remove all track assignments?",
            "Clear All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        StopCurrentPlayback();
        foreach (var t in Tracks) { t.PcmPath = null; t.ValidationError = null; }
        SetDirty();
        OnPropertyChanged(nameof(AssignedCountText));
        OnPropertyChanged(nameof(CanApply));
        AppendLog("All track assignments cleared.");
    }

    // ── Launch Settings ───────────────────────────────────────────────────
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

    private void TrackerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            TrackerUrl = item.Tag as string;
    }

    private void OpenTracker_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TrackerUrl))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = TrackerUrl, UseShellExecute = true })?.Dispose();
    }

    // ── Launch ────────────────────────────────────────────────────────────
    private async Task LaunchRomCoreAsync(AutoLaunchOption option)
    {
        if (_lastOutputRomPath is null || !File.Exists(_lastOutputRomPath)) return;

        if (option is AutoLaunchOption.ArchipelagoOnly or AutoLaunchOption.ArchipelagoAndTracker)
            await LaunchArchipelagoAsync();

        if (option is AutoLaunchOption.TrackerOnly or AutoLaunchOption.ArchipelagoAndTracker)
            LaunchTracker();

        LaunchEmulator();
    }

    private async Task LaunchArchipelagoAsync()
    {
        // Start SNI if configured and not already running
        if (!string.IsNullOrEmpty(_sniPath) && File.Exists(_sniPath))
        {
            var procs = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_sniPath));
            bool sniRunning = procs.Length > 0;
            foreach (var p in procs) p.Dispose();

            if (!sniRunning)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = _sniPath, UseShellExecute = true })?.Dispose();
                await System.Threading.Tasks.Task.Delay(1000); // let SNI initialize
                AppendLog("SNI started.");
            }
        }

        // Start Archipelago Launcher if configured and not already running
        if (!string.IsNullOrEmpty(_archipelagoLauncherPath) && File.Exists(_archipelagoLauncherPath))
        {
            var procs = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_archipelagoLauncherPath));
            bool archRunning = procs.Length > 0;
            foreach (var p in procs) p.Dispose();

            if (!archRunning)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = _archipelagoLauncherPath, UseShellExecute = true })?.Dispose();
                AppendLog("Archipelago Launcher started.");
            }
        }
    }

    private void LaunchTracker()
    {
        if (!string.IsNullOrEmpty(_trackerUrl))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = _trackerUrl, UseShellExecute = true })?.Dispose();
            AppendLog("Tracker opened.");
        }
    }

    private void LaunchEmulator()
    {
        try
        {
            if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath))
            {
                // --lua= must come BEFORE the ROM path for BizHawk
                var args = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(_connectorScriptPath) && File.Exists(_connectorScriptPath))
                    args.Append($"--lua=\"{_connectorScriptPath}\" ");
                args.Append($"\"{_lastOutputRomPath}\"");

                var dir = Path.GetDirectoryName(_emulatorPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _emulatorPath,
                    Arguments = args.ToString(),
                    WorkingDirectory = dir ?? "",
                    UseShellExecute = false
                })?.Dispose();
                AppendLog($"Launched: {Path.GetFileName(_emulatorPath)}");
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = _lastOutputRomPath, UseShellExecute = true })?.Dispose();
                AppendLog($"Launched: {Path.GetFileName(_lastOutputRomPath)}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Launch failed: {ex.Message}");
        }
    }

    // ── Log ───────────────────────────────────────────────────────────────
    private void AppendLog(string line)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            LogBox.AppendText($"[{ts}] {line}\n");
            LogBox.ScrollToEnd();
        });
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Wraps an async Task action so exceptions are logged instead of silently lost
    /// in async void event handlers (e.g. MenuItem.Click lambdas).
    /// </summary>
    private async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] {ex.Message}");
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    protected override void OnClosed(EventArgs e)
    {
        _closeCts.Cancel();
        _closeCts.Dispose();
        _applyEngine.ConflictsDetected -= OnConflictsDetected;
        _audio.Dispose();
        base.OnClosed(e);
    }
}
