using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using LTTPEnhancementTools.Models;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools;

public partial class SpriteBrowserWindow : Window
{
    // ── Static shared state ───────────────────────────────────────────────
    private static readonly HttpClient Http = Services.SharedHttp.Client;
    private static List<SpriteEntry>? _cachedSprites;

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "SpriteCache");

    public static readonly string SpritesListCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "sprites_list.json");

    /// <summary>Preview URL for the "Link Nintendo" (default) sprite. Set after first sprite list load.</summary>
    public static string? DefaultLinkPreviewUrl { get; private set; }

    private const string DefaultSpriteName = "Link";

    /// <summary>Hardcoded preview URL for the default Link sprite — used when sprites list is not yet cached.</summary>
    public const string DefaultLinkPreviewFallbackUrl =
        "https://alttpr-assets.s3.us-east-2.amazonaws.com/001.link.1.zspr.png";

    /// <summary>Sentinel stored as SpritePath when "Random All" is chosen. Resolved to a real sprite at apply time.</summary>
    public const string RandomAllSentinel = "__random_all__";

    /// <summary>Sentinel stored as SpritePath when "Random Favorites" is chosen. Resolved to a real sprite at apply time.</summary>
    public const string RandomFavoritesSentinel = "__random_favorites__";

    private static readonly JsonSerializerOptions JsonOptions = Services.JsonDefaults.ReadOnly;

    private const string SpritesApiUrl = "https://alttpr.com/sprites";

    // ── Instance state ────────────────────────────────────────────────────
    private ICollectionView? _view;
    private string _searchText = string.Empty;
    private HashSet<string> _favorites = new();

    /// <summary>Set after the user clicks "Select Sprite". Null if cancelled.</summary>
    public string? SelectedSpritePath { get; private set; }

    /// <summary>Preview image URL for the selected sprite. Null if cancelled.</summary>
    public string? SelectedSpritePreviewUrl { get; private set; }

    /// <summary>True when the user selected the default Link Nintendo sprite (reset to default).</summary>
    public bool SelectedIsDefault { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────
    public SpriteBrowserWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _favorites = FavoritesManager.Load();
        await LoadSpritesAsync();
    }

    // ── Data loading ──────────────────────────────────────────────────────
    private async Task LoadSpritesAsync(bool forceRefresh = false)
    {
        try
        {
            if (_cachedSprites == null || forceRefresh)
                _cachedSprites = await FetchSpriteListAsync(forceRefresh);

            // Apply saved favorites (re-apply each open so disk changes are picked up)
            foreach (var entry in _cachedSprites)
                entry.IsFavorite = _favorites.Contains(entry.Name);

            // ListCollectionView with live shaping: automatically re-sorts when IsFavorite
            // changes via PropertyChanged — no manual Refresh() needed after starring.
            var lcv = new ListCollectionView(_cachedSprites);
            lcv.Filter = FilterSprite;
            lcv.SortDescriptions.Add(new SortDescription(nameof(SpriteEntry.IsFavorite), ListSortDirection.Descending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(SpriteEntry.Name), ListSortDirection.Ascending));
            lcv.IsLiveSorting = true;
            lcv.LiveSortingProperties.Add(nameof(SpriteEntry.IsFavorite));
            _view = lcv;
            SpriteList.ItemsSource = _view;

            // Capture the default Link preview URL for use in the main window
            if (DefaultLinkPreviewUrl is null)
                DefaultLinkPreviewUrl = _cachedSprites
                    .FirstOrDefault(s => s.Name == DefaultSpriteName)?.Preview;

            LoadingText.Visibility = Visibility.Collapsed;
            RandomCardsPanel.Visibility = Visibility.Visible;
            SpriteList.Visibility = Visibility.Visible;
            StatusText.Text = $"{_cachedSprites.Count} sprites";
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Failed to load sprites:\n{ex.Message}";
            StatusText.Text = string.Empty;
        }
    }

    private async Task<List<SpriteEntry>> FetchSpriteListAsync(bool forceRefresh)
    {
        bool hasDiskCache = File.Exists(SpritesListCachePath);

        // Load from disk cache if not forcing refresh and cache is available
        if (!forceRefresh && hasDiskCache)
        {
            StatusText.Text = "Loading sprite list…";
            var cached = await File.ReadAllTextAsync(SpritesListCachePath);
            return JsonSerializer.Deserialize<List<SpriteEntry>>(cached, JsonOptions) ?? new();
        }

        // Fetch from web
        StatusText.Text = forceRefresh ? "Refreshing from alttpr.com…" : "Fetching sprite list…";
        try
        {
            var json = await Http.GetStringAsync(SpritesApiUrl);
            var list = JsonSerializer.Deserialize<List<SpriteEntry>>(json, JsonOptions) ?? new();
            _ = SaveSpriteListAsync(json);
            return list;
        }
        catch
        {
            // Offline fallback: use cached list if available
            if (hasDiskCache)
            {
                StatusText.Text = "Offline — showing cached list";
                var cached = await File.ReadAllTextAsync(SpritesListCachePath);
                return JsonSerializer.Deserialize<List<SpriteEntry>>(cached, JsonOptions) ?? new();
            }
            throw; // no cache, propagate to show error
        }
    }

    private static async Task SaveSpriteListAsync(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SpritesListCachePath)!);
            await File.WriteAllTextAsync(SpritesListCachePath, json);
        }
        catch { }
    }

    private bool FilterSprite(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (obj is not SpriteEntry entry) return false;

        var q = _searchText.Trim();
        if (entry.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (entry.Author.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var tag in entry.Tags)
            if (tag.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    // ── Search ────────────────────────────────────────────────────────────
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        _view?.Refresh();

        if (_view != null)
        {
            int count = 0;
            foreach (var _ in _view) count++;
            int total = _cachedSprites?.Count ?? 0;
            StatusText.Text = count == total
                ? $"{total} sprites"
                : $"{count} / {total} sprites";
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────
    private void SpriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = SpriteList.SelectedItem is SpriteEntry;
    }

    private void SpriteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SpriteList.SelectedItem is SpriteEntry)
            SelectSprite_Click(sender, e);
    }

    // ── Favorites ─────────────────────────────────────────────────────────
    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SpriteEntry entry) return;

        entry.IsFavorite = !entry.IsFavorite;
        if (entry.IsFavorite)
            _favorites.Add(entry.Name);
        else
            _favorites.Remove(entry.Name);

        FavoritesManager.Save(_favorites);
        // Live shaping handles the re-sort automatically via PropertyChanged — no Refresh() needed.

        e.Handled = true; // don't bubble to ListBoxItem selection
    }

    // ── Refresh ───────────────────────────────────────────────────────────
    private async void RefreshList_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        _cachedSprites = null; // force re-fetch from web
        await LoadSpritesAsync(forceRefresh: true);
        RefreshButton.IsEnabled = true;
    }

    // ── Select Sprite ─────────────────────────────────────────────────────
    private async void SelectSprite_Click(object sender, RoutedEventArgs e)
    {
        if (SpriteList.SelectedItem is not SpriteEntry entry) return;

        // "Link Nintendo" is the default sprite — selecting it resets to no custom sprite
        if (entry.Name == DefaultSpriteName)
        {
            SelectedIsDefault = true;
            DialogResult = true;
            Close();
            return;
        }

        SelectButton.IsEnabled = false;
        StatusText.Text = "Downloading sprite…";

        try
        {
            Directory.CreateDirectory(CacheDir);

            var safeName = string.Concat(entry.Name.Split(Path.GetInvalidFileNameChars()));
            var localPath = Path.Combine(CacheDir, safeName + ".zspr");

            if (!File.Exists(localPath))
            {
                var data = await Http.GetByteArrayAsync(entry.File);
                await File.WriteAllBytesAsync(localPath, data);
            }

            SelectedSpritePath = localPath;
            SelectedSpritePreviewUrl = entry.Preview;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Download failed: {ex.Message}";
            SelectButton.IsEnabled = true;
        }
    }

    // ── Random selection ──────────────────────────────────────────────────
    private void RandomAll_Click(object sender, RoutedEventArgs e)
    {
        SelectedSpritePath = RandomAllSentinel;
        DialogResult = true;
        Close();
    }

    private void RandomFavorites_Click(object sender, RoutedEventArgs e)
    {
        SelectedSpritePath = RandomFavoritesSentinel;
        DialogResult = true;
        Close();
    }

    // ── Cancel ────────────────────────────────────────────────────────────
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _view = null;
    }
}
