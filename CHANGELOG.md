# Changelog

All notable changes to ALttP Enhancement Tools are documented here.

---

## [2.4.0] — 2026-03-28

### Added
- **Auto Launch dialog** — replaces the old "Apply & Launch" and "Launch ROM" buttons with a single **Auto Launch** button that opens a dialog to choose: Tracker Only, Archipelago Only, or Archipelago + Tracker; the emulator always launches alongside your selection
- **Vanilla ROM blocking** — selecting a vanilla (1 MB) ROM now shows a warning and blocks it; only randomized (2 MB) ROMs are accepted, ensuring MSU-1 compatibility
- **Smart music conflict handling** — applying to a folder with existing PCM files now prompts you to overwrite, keep existing tracks, or cancel; ROM and `.msu` files are always updated silently (no unnecessary prompts)
- **Shared utilities** — new `JsonDefaults` and `SharedHttp` classes consolidate duplicate `JsonSerializerOptions` (was in 6 files) and `HttpClient` instances (was in 3 files)
- **Unit tests** — new test project with 42 tests covering PcmValidator, OriginalSoundtrackManager matching, and TrackSlot model

### Changed
- **Bottom action bar** — streamlined from 4 buttons (Select ROM, Apply to ROM, Apply & Launch, Launch ROM) to 3 (Select ROM, Apply ROM, Auto Launch)
- **Launch logic refactored** — split monolithic launch method into composable `LaunchArchipelagoAsync`, `LaunchTracker`, and `LaunchEmulator` methods
- **PCM assignment deduplicated** — extracted shared `ValidateAndAssignPcm` and `ConvertAndAssignPcmAsync` helpers, replacing ~100 lines of duplicate logic

### Fixed
- **ROM overwrite bug** — ROM and `.msu` files now always use `overwrite: true` regardless of conflict dialog choice; previously could throw if user picked "Skip" for PCMs
- **Process handle leaks** — all `Process.Start()` and `GetProcessesByName()` calls now properly dispose returned `Process` objects
- **Async void exception loss** — `PickPcmForSlot` changed from `async void` to `async Task` wrapped with `SafeAsync` error handler
- **Window close cancellation** — long-running apply operations now cancel when the window closes instead of continuing in the background
- **Empty catch blocks** — persistence managers (Settings, LaunchSettings, AutoSave, Favorites) now log failures via `Debug.WriteLine` instead of silently swallowing exceptions
- **Null-forgiving operator** — removed unsafe `Path.GetDirectoryName(...)!` in `LaunchEmulator`, replaced with null-coalescing fallback

---

## [2.3.0] — 2026-03-14

### Added
- **Original soundtrack preview** — import the original ALttP soundtrack from a folder, ZIP, or URL; files are auto-matched to slots and converted to MSU-1 PCM; click **♪** to preview
- **Track type labels** — each slot shows **[SFX]** or **[EXT]** badges for jingles and extended tracks
- **Smart OST matching** — 84-entry alias table maps common OST names to the correct MSU slots; 4-pass matching (alias → leading number → any number → fuzzy name)

---

## [2.2.0] — 2026-03-07

### Added
- **Random sprite selection** — choose Random All or Random Favorites from the sprite browser; the actual sprite is picked at apply time as a surprise

---

## [2.1.0] — 2026-02-28

### Changed
- **Branding update** — new logo, icon, and header redesign

---

## [2.0.0] — 2026-02-24

### Added
- **Pack Export / Import (`.lttppack`)** — bundle all assigned PCM tracks into a shareable archive; recipients import it to extract the files and load the playlist in one step
- **Path traversal protection** — ZIP import now validates extracted paths stay within the destination folder
- **User FAQ / Help doc** — new `HELP.md` covering getting started, music, sprites, playlists, troubleshooting, and FAQ including SmartScreen guidance

### Changed
- **README** — added prominent "Bring Your Own ROM" notice, expanded legal disclaimer into a full Legal & Copyright section, documented Playlists and Pack Export/Import
- **Removed Nintendo-owned image** from source tree (was not part of the app but was present in the repo)
- **CleanOutputFolder** no longer blocks the UI thread during file deletion

### Fixed
- `CancellationTokenSource` in `SpriteImageControl` was cancelled but never disposed on rapid URL changes (minor memory leak)
- Unused `using Button` type alias removed from `MainWindow.xaml.cs`
- `ListCollectionView` reference released when `SpriteBrowserWindow` closes

---

## [1.9.0] — 2026-02-24

### Added
- **Sprite browser card grid** — 600+ community sprites displayed as thumbnail cards with name and author; replaces the old plain list
- **Animated triforce loading indicator** — each sprite card shows a cascading triforce outline animation while its preview image loads
- **Sprite favorites** — star button on each card pins sprites to the top of the list; favorites persist across sessions in `%LocalAppData%\LTTPEnhancementTools\sprite_favorites.json`
- **Offline sprite browser** — sprite list and all preview images are cached to disk after first load; the browser opens instantly and works fully offline on subsequent uses
- **↻ Refresh button** — re-fetches the sprite list from alttpr.com when you want to pick up newly added sprites
- **Default Link preview** — Link's sprite thumbnail is shown in the main window when no custom sprite is selected; downloaded and cached automatically on startup
- **"Link" resets to default** — selecting the default "Link" entry in the sprite browser clears any custom sprite selection

### Fixed
- Sprite preview images in the browser were never loading (WPF's `BitmapImage.UriSource` silently fails for HTTPS in .NET 8); fixed by downloading via `HttpClient` and loading from `MemoryStream`
- Main window sprite preview was always blank (a `MemoryStream` was disposed before `BitmapImage.EndInit()` was called — the stream scope closed inside the `if/else` block but `EndInit()` was called after)
- Stale web preview URLs saved to auto-state before caching completed (e.g. after a crash) now re-download and cache correctly on next launch

---

## [1.8.0] — 2025-01-xx

### Added
- Launcher UI redesign
- Playlists support
- Auto-save of last session state (ROM, sprite, playlist)

---

## [1.7.0]

### Added
- First-run setup wizard (4-step: emulator, connector, SNI, tracker)
- Separate launch settings file (`launchSettings.json`) independent of app settings
- Sprite-only apply mode (no music tracks required)
- Dark theme ComboBox style fix

---

## [1.6.1]

### Fixed
- Library play button not working
- Save/Load config dialogs now default to the app's `Configs/` folder

---

## [1.6.0]

### Added
- Track search — filter the 61-slot list by name
- Library slot header
- Clear All button
- Overwrite by default behavior

---

## [1.5.0]

### Added
- Dedicated output folder
- Launch ROM button
- Sprite-only apply
- Sprite image display fix

---

## Earlier versions

See git history for changes prior to v1.5.0.
