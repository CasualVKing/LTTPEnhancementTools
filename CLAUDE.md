# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Run in development
dotnet run

# Build self-contained single-file EXE
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false
# Output: bin\Release\net8.0-windows\win-x64\publish\LTTPEnhancementTools.exe

# Build EXE + Inno Setup installer (Windows only, requires Inno Setup 6 installed)
publish.bat

# Build only (no publish)
dotnet build -c Release
```

```bash
# Run tests
dotnet test LTTPEnhancementTools.Tests/LTTPEnhancementTools.Tests.csproj
```

**CI**: `.github/workflows/test.yml` runs the test suite on every push/PR to main. Push a tag matching `v*.*.*` to trigger `.github/workflows/release.yml`, which builds the EXE + installer and publishes a GitHub Release. The workflow passes `/dMyVersion=X.Y.Z` to Inno Setup via the tag name.

## What the App Does

An **Archipelago enhancer** for A Link to the Past. Users receive `.aplttp` patch files from an Archipelago multiworld; this tool generates/locates the `.sfc`, adds MSU-1 music and a custom Link sprite **in-place**, then launches the full play stack (SNI → Archipelago SNI client → web tracker → emulator) from one "Enhance & Launch" button.

## Architecture

Single-window WPF application targeting .NET 8.0 Windows x64. `MainWindow` acts as both view and view-model (MVVM-lite — no separate ViewModel class). `DataContext = this` is set in the constructor. NuGet deps: `NAudio` (audio conversion/playback), `BsDiff` (bsdiff4 patch application).

### Key Patterns

**Services are static/stateless** (`ArchipelagoPatchReader`, `PcmConverter`, `PcmValidator`, `SpriteApplier`, `OriginalSoundtrackManager`, `PlaylistManager`, `PlaylistBundleManager`, plus the settings managers). Fallible operations return `string?` — `null` means success, non-null is an error message — or a `(result, error)` tuple. This is the standard error-handling idiom throughout.

**`ApplyEngine`** (in `Services/MsuApplyEngine.cs` — note the class/file name mismatch) is the main stateful service. It orchestrates pack assembly asynchronously and uses `ConflictsDetectedEventArgs` with a `TaskCompletionSource` to pause execution while the UI resolves file conflicts. The UI subscribes to `ConflictsDetected`, sets `e.Resolution`, then calls `e.Complete()`. (MainWindow currently passes `OverwriteMode.Skip`, so the event only fires for `Ask` mode.)

**Observable state** in `MainWindow`: properties fire `OnPropertyChanged()`. `CanApply` is a computed bool many properties depend on — whenever relevant state changes, callers must explicitly call `OnPropertyChanged(nameof(CanApply))` (same for `CanExportPack`). The constructor writes backing fields directly during restore, then raises a notification batch at the end, to avoid triggering saves mid-initialization.

**Track catalog** (61 ALttP slot-to-name mappings, with `music`/`jingle`/`extended` types) is loaded from `Resources/trackCatalog.json` as an embedded WPF resource (`pack://application:,,,/Resources/trackCatalog.json`). Resources must use the `<Resource>` build action in the .csproj to work in single-file publish.

**Shared singletons**: `Services.SharedHttp.Client` is the one `HttpClient`; `Services.JsonDefaults.Standard/ReadOnly` are the shared `JsonSerializerOptions`; `Services.PreviewCache.GetPath(url)` maps preview URLs to disk-cache paths (SHA256 fallback — never `string.GetHashCode()`, which is randomized per process).

### Archipelago Patch Flow (ArchipelagoPatchReader)

`.aplttp` files are ZIP archives containing `archipelago.json` (server, player, player_name, game, base_checksum) and `delta.bsdiff4`. `ReadPatch()` extracts metadata and computes `ExpectedSfcPath` (same directory, same stem). If the `.sfc` doesn't exist yet, `ApplyPatch()` validates the base ROM's MD5 against `base_checksum` and applies the bsdiff4 patch itself (prompting for a vanilla ROM if `BaseRomPath` isn't configured). Patch output is written to a `.tmp` file and moved into place atomically — a failed apply must never leave a partial `.sfc`, because callers treat `File.Exists(ExpectedSfcPath)` as "already patched".

### Apply Workflow (ApplyEngine.RunAsync)

1. Validate inputs (ROM exists, all PCM files exist, sprite valid if provided)
2. Compute output filenames from `OutputBaseName` (fallback: ROM stem)
3. Detect PCM conflicts → fire event → wait for resolution (ROM/.msu always overwrite)
4. Create output directory
5. If `InPlace` (the normal path): use the source ROM directly; otherwise copy it
6. *(Optional)* `SpriteApplier.Apply` patches the ROM; in-place mode passes `preserveOriginal: true`
7. Write empty `.msu` marker (only when tracks are assigned)
8. Copy each PCM to `{baseName}-{slot}.pcm`

Before applying, MainWindow resolves the random-sprite sentinels (see below) to a concrete `.zspr` path.

### ZSPR Sprite Injection (SpriteApplier)

ROM write targets (from pyz3r reference): `0x80000` gfx (≤ 0x7000 bytes), `0xDD308` palette (120 bytes), `0xDEDF5` gloves (4 bytes). ZSPR header: magic `"ZSPR"` at byte 0; gfx offset (uint32 LE) at 9; gfx length (ushort LE) at 13; palette offset (uint32 LE) at 15; palette length (ushort LE) at 19; last 4 palette bytes are gloves. Legacy `.spr` files are raw gfx only.

Writes go to a `.tmp` file, then replace the ROM atomically. With `preserveOriginal: true` (in-place applies), the ROM's original sprite regions are backed up to a `{rom}.spritebak` sidecar on first apply and restored before each subsequent apply, so switching to a sprite with shorter gfx doesn't inherit residue from the previous one.

### Sprite Browser & Random Sprites

`SpriteBrowserWindow` fetches `https://alttpr.com/sprites` (~600 sprites), caches the JSON to `%LocalAppData%\LTTPEnhancementTools\sprites_list.json` (offline fallback) and the list in a static field for the session. Downloaded `.zspr` files cache to `%LocalAppData%\LTTPEnhancementTools\SpriteCache\{name}.zspr`; preview PNGs to `SpriteCache\Previews\` via `PreviewCache.EnsureCachedAsync`, which self-heals: writes are atomic, bytes are only cached after they decode as a valid image, and a cached file that fails to decode is deleted and re-downloaded. When a server preview is missing (~18% of the catalog), `SpriteThumbnailRenderer` generates one locally from the `.zspr`'s own pixel data — SNES 4bpp planar tile decode, green-mail palette, standing pose composited as head block (0,0) over body block (0,1) with the body offset 8px down, matching the official previews' 16x24 layout. `SpriteImageControl` falls back to this via its `SpriteFileUrl` property and shows a "no preview" label only if that also fails. Favorites persist through `FavoritesManager` (`sprite_favorites.json`); the list live-sorts favorites first via `ListCollectionView` live shaping.

Selecting "Random" stores a sentinel in `SpritePath` (`SpriteBrowserWindow.RandomAllSentinel` / `RandomFavoritesSentinel`) which `MainWindow.PickRandomSpriteAsync` resolves to a real downloaded sprite at apply time. Selecting the default "Link" sprite clears the custom sprite. `SpriteImageControl` is the async preview image control (per-control CTS cancels stale downloads).

### Music: Library, Playlists, Packs, Originals

- **`MusicLibrary`**: folder of audio files (default: `MusicLibrary\` next to the EXE) scanned into `LibraryEntry` items; non-PCM sources convert on first assign into `{library}\_cache\{name}.pcm` (cache invalidated when the source is newer).
- **`PlaylistManager`**: slot→path JSON playlists (version 1) saved under `{library}\Playlists\`; paths normalized to forward slashes.
- **`PlaylistBundleManager`**: `.lttppack` export/import — a ZIP of `manifest.json` + `tracks/NN.pcm` (stored uncompressed; PCM doesn't compress well). Import extracts into `{library}\Imported\{packName}\` with zip path-traversal guards and applies as an unsaved session.
- **`OriginalSoundtrackManager`**: imports the vanilla OST from folder/ZIP/URL for A/B preview. Matches files to slots in 4 passes (OST alias table → leading number → any number → fuzzy name), converts via `PcmConverter`, caches to `%LocalAppData%\LTTPEnhancementTools\OriginalAudio\NN.pcm`.

### MSU-1 PCM Format

8-byte header: `"MSU1"` (4 ASCII bytes) + loop point (uint32 LE), then raw 44.1 kHz 16-bit stereo PCM. `AudioPlayer` (NAudio, single playback channel) skips the header when playing; `PcmConverter` (MediaFoundation resampler, AIFF via `AiffFileReader`) writes it when converting.

### Launch Stack (MainWindow)

"Enhance & Launch" runs the apply, then: `EnsureSniRunningAsync` starts SNI if not running → launches `ArchipelagoSNIClient.exe` (found next to `ArchipelagoLauncher.exe`) with `--connect <server>` (falls back to the launcher itself) → opens the tracker URL → starts the emulator with `--lua=<connector>` before the ROM path (BizHawk argument order matters). Process handles are disposed; already-running processes are detected by name and skipped.

### Settings & Persistence (all in `%LocalAppData%\LTTPEnhancementTools\`)

| File | Manager | Contents |
|------|---------|----------|
| `settings.json` | `SettingsManager` | library folder |
| `launchSettings.json` | `LaunchSettingsManager` | emulator, connector script, SNI, Archipelago launcher, tracker URL, seed URL, base ROM path |
| `autoSave.json` | `AutoSaveManager` | last sprite/preview/playlist/patch for session restore |
| `sprite_favorites.json` | `FavoritesManager` | favorite sprite names |
| `crash.log` | `App.xaml.cs` | global exception log (dispatcher + domain + unobserved task) |

`SetupWizardWindow` runs on first launch (when `launchSettings.json` doesn't exist) and can be re-run from the Auto Launcher section. **Important**: the wizard only edits some launch settings — it must carry through `BaseRomPath`/`SeedUrl` from the existing settings when saving (see `SaveAndClose`), or re-running it wipes them.

## Project Structure

```
Models/          TrackSlot, ArchipelagoMetadata, LibraryEntry, Playlist, SpriteEntry, AutoLaunchOption
Services/        ArchipelagoPatchReader, MsuApplyEngine (class ApplyEngine), SpriteApplier,
                 PcmConverter, PcmValidator, AudioPlayer, OriginalSoundtrackManager,
                 MusicLibrary, PlaylistManager, PlaylistBundleManager,
                 SettingsManager/AppSettings, LaunchSettingsManager/LaunchSettings,
                 AutoSaveManager/AutoSaveState, FavoritesManager, JsonDefaults (+SharedHttp), PreviewCache
Controls/        SpriteImageControl (async cached preview image)
Converters/      ValueConverters.cs (WPF IValueConverter implementations)
Resources/       Styles.xaml (dark theme), trackCatalog.json, icon.ico, lttpEnhancedLogo.png
App.xaml(.cs)    Global exception handler → crash.log
MainWindow.*     Main UI + ViewModel (largest file — view, apply flow, library, playlists, launch stack)
SpriteBrowserWindow.*  Modal sprite picker (search, favorites, random)
SetupWizardWindow.*    First-run launch-settings wizard
setup.iss        Inno Setup 6 installer script (per-user install, no admin required)
LTTPEnhancementTools.Tests/  xUnit tests for the service layer (run in CI)
```
