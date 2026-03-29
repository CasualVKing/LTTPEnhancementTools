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

**GitHub Release**: Push a tag matching `v*.*.*` to trigger the release workflow, which builds and publishes artifacts automatically. The workflow passes `/dMyVersion=X.Y.Z` to Inno Setup via the tag name.

## Architecture

Single-window WPF application targeting .NET 8.0 Windows x64. `MainWindow` acts as both view and view-model (MVVM-lite — no separate ViewModel class). `DataContext = this` is set in the constructor.

### Key Patterns

**Services are static/stateless** (`ConfigManager`, `PcmConverter`, `PcmValidator`, `SpriteApplier`). All return `string?` — `null` means success, non-null is an error message. This is the standard error-handling idiom throughout.

**`ApplyEngine`** is the only stateful service. It orchestrates pack assembly asynchronously and uses a `ConflictsDetectedEventArgs` with a `TaskCompletionSource` to pause execution and wait for the UI to resolve file conflicts. The UI subscribes to `ConflictsDetected`, shows a dialog, sets `e.Resolution`, then calls `e.Complete()`.

**Observable state** in `MainWindow`: properties fire `OnPropertyChanged()` to update the UI. `CanApply` is a computed bool that many properties depend on — whenever relevant state changes, callers must explicitly call `OnPropertyChanged(nameof(CanApply))`.

**Track catalog** (61 ALttP slot-to-name mappings) is loaded from `Resources/trackCatalog.json` as an embedded WPF resource (`pack://application:,,,/Resources/trackCatalog.json`). The same pattern applies to `Resources/icon.ico` — it must be a `<Resource>` build action in the .csproj to work in single-file publish.

### Apply Workflow (MsuApplyEngine)

`RunAsync` steps in order:
1. Validate inputs (ROM exists, all PCM files exist, sprite file valid if provided)
2. Compute output filenames using `OutputBaseName` (or fallback to ROM stem)
3. Detect conflicts → fire event → wait for resolution
4. `Directory.CreateDirectory`
5. Copy ROM to `{baseName}{romExt}`
6. *(Optional)* `SpriteApplier.Apply` patches the copied ROM in-place
7. Write empty `.msu` marker file
8. Copy each PCM to `{baseName}-{slot}.pcm` (sorted by slot number)

### ZSPR Sprite Injection (SpriteApplier)

ROM write targets (from pyz3r reference):
- `0x80000` — pixel/graphics data (≤ 0x7000 bytes)
- `0xDD308` — palette data (120 bytes; last 4 bytes are gloves)
- `0xDEDF5` — gloves palette (4 bytes)

ZSPR header layout: magic `"ZSPR"` at byte 0; gfx offset (uint32 LE) at byte 9; gfx length (ushort LE) at byte 13; palette offset (uint32 LE) at byte 15; palette length (ushort LE) at byte 19.

Legacy `.spr` files are raw gfx data only — no header, no palette.

### Sprite Browser (SpriteBrowserWindow)

Fetches `https://alttpr.com/sprites` (JSON array of ~600+ sprites) on open; list is cached in the static field `_cachedSprites` for the session. Downloaded `.zspr` files are cached to `%LocalAppData%\LTTPEnhancementTools\SpriteCache\{name}.zspr`. Uses a class-level static `HttpClient`.

### MSU-1 PCM Format

Files have an 8-byte header: `"MSU1"` (4 ASCII bytes) + loop point (uint32 LE). Audio follows as raw 44.1 kHz, 16-bit, stereo PCM. `AudioPlayer` skips the header when playing. `PcmConverter` writes it when converting from other formats.

### Config Schema (AppConfig.cs)

JSON fields: `version` (int, always 1), `romPath`, `spritePath`, `tracks` (object: slot string → pcm path string), `lastModified`. Paths are normalized to forward slashes in `ConfigManager.Save()`.

## Project Structure

```
Models/          TrackSlot, AppConfig, SpriteEntry
Services/        AudioPlayer, ConfigManager, MsuApplyEngine, PcmConverter, PcmValidator, SpriteApplier
Converters/      ValueConverters.cs (5 WPF IValueConverter implementations)
Resources/       Styles.xaml (dark theme), trackCatalog.json, icon.ico
App.xaml(.cs)    Global exception handler → %LocalAppData%\LTTPEnhancementTools\crash.log
MainWindow.*     Main UI + ViewModel
SpriteBrowser.*  Modal sprite picker window
setup.iss        Inno Setup 6 installer script (per-user install, no admin required)
```
