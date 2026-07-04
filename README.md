# Archipelago LTTP Enhancer

[![Tests](https://github.com/CasualVKing/LTTPEnhancementTools/actions/workflows/test.yml/badge.svg)](https://github.com/CasualVKing/LTTPEnhancementTools/actions/workflows/test.yml)

![LTTP Enhanced](Resources/lttpEnhancedLogo.png)

A Windows desktop utility for enhancing **Archipelago A Link to the Past** runs. Load your `.aplttp` patch file, apply MSU-1 music packs and custom Link sprites, then launch your entire Archipelago session — emulator (with Lua connector), SNI, SNI client, and tracker — in one click.

> **Bring Your Own ROM.** This tool requires you to provide your own legally-obtained ALttP ROM. It does not include, distribute, or circumvent any copyrighted game data. See [Legal & Copyright](#legal--copyright) below.

---

## Features

### Archipelago Integration
- **`.aplttp` patch loading** — open your Archipelago patch file directly; the tool extracts player name, server address, and game metadata automatically
- **Built-in patch application** — applies the bsdiff4 patch from the `.aplttp` to your base ROM automatically; no need to open the patch in Archipelago Launcher first
- **Base ROM configuration** — set your vanilla ALttP ROM path once; validated via MD5 checksum; prompted on first use
- **One-click Enhance & Launch** — applies music + sprite in-place, then launches SNI client, community tracker, and emulator in sequence
- **Server auto-fill** — the Room ID field is populated from the patch metadata
- **Session memory** — your last patch, sprite, and playlist are remembered across launches

### Music
- **61-slot track list** — all ALttP music slots listed by name, including extended dungeon and boss tracks; each slot shows a **[SFX]** or **[EXT]** badge so you can see at a glance which are jingles, extended/optional, or full music tracks
- **Original soundtrack preview** — import the original ALttP soundtrack (MP3, WAV, FLAC, etc.) from a folder, ZIP, or URL; files are auto-matched to the correct slots by name or number and converted to MSU-1 PCM; click **♪** on any track to hear the original for comparison
- **Smart track matching** — handles common OST releases (including the Internet Archive soundtrack) with an alias table that maps names like "Hyrule Field Main Theme" to Light World, "Dark Golden Land" to Dark World, etc.; also matches by track number and fuzzy name
- **Music Library** — a dedicated `MusicLibrary/` folder next to the app stores all your audio files in one place; songs appear in a per-slot picker popup with instant preview
- **Audio conversion built-in** — import MP3, WAV, WMA, AAC, M4A, AIFF, and more; files are converted to MSU-1 PCM automatically and cached
- **Preview playback** — listen to any assigned track before applying
- **Track search** — filter the 61-slot track list by name; **Clear All** removes all assignments at once
- **Playlists** — save and load your full set of track assignments as `.json` playlist files; auto-saved between sessions
- **Pack Export / Import** — bundle your assigned PCM tracks into a shareable `.lttppack` file; import a pack to instantly load all its tracks

### Sprite
- **Link Sprite replacement** — apply a custom `.zspr` or `.spr` Link sprite to your ROM
- **Online Sprite Browser** — browse 600+ community sprites in a card grid with live preview thumbnails
- **Local thumbnail rendering** — when a sprite's preview image is missing from alttpr.com (true for ~18% of the catalog), a thumbnail is generated directly from the sprite's own pixel data, so every sprite gets a real preview
- **Random Sprite** — choose **Random All** (**?**) or **Random Favorites** (**?★**); a sprite is picked at apply time as a surprise
- **Favorites** — star any sprite to pin it to the top of the list; favorites persist across sessions
- **Offline support** — sprite list and all preview images are cached to disk after first load
- **Lossless sprite switching** — the ROM's original sprite data is backed up to a small `.spritebak` file next to your ROM on first apply, so changing sprites never leaves leftovers from the previous one

### Auto Launch
- **Setup Wizard** — on first run a 5-step wizard walks you through configuring your emulator, Archipelago connector script, Archipelago Launcher, SNI, and community tracker
- **BizHawk integration** — auto-passes `--lua=<connector>` before the ROM path so the Archipelago Lua script starts automatically
- **SNI auto-start** — SNI.exe is started before the emulator if it isn't already running
- **Community tracker** — choose from Archipelago's built-in tracker, Dunka's Tracker, or alttprtracker.com; opens in the browser on launch
- **Re-run wizard** — reconfigure any path at any time via the **⚙ Run Setup Wizard…** button

### General
- **In-place enhancement** — sprite patches the `.sfc` directly; MSU files are written next to the ROM with matching names
- **Non-destructive re-runs** — existing PCM files are kept as-is when you re-run Enhance & Launch; only newly assigned tracks are written
- **Auto-save session** — your last patch, sprite, and playlist are remembered and restored on next launch
- **Robust file handling** — patched ROMs and cached images are written atomically; interrupted writes can't leave corrupt files behind, and damaged cache entries repair themselves automatically
- **No admin rights required** — per-user install, no elevated permissions needed
- **No .NET runtime required** — ships as a single self-contained EXE

---

## System Requirements

- Windows 10 version 1809 (October 2018 Update) or later / Windows 11
- 64-bit (x64) processor
- ~80 MB disk space for the installed app

> You must provide your own legally-obtained ALttP ROM (`.sfc` or `.smc`). This tool does not include any ROM files or copyrighted game assets.

---

## Installation

Download the latest installer (`LTTPEnhancementToolsSetup-*-win64.exe`) from the [Releases](../../releases/latest) page and run it. No administrator password needed — it installs to your personal `AppData\Local\Programs` folder.

Alternatively, grab just the standalone `LTTPEnhancementTools.exe` and run it from anywhere.

> **Windows SmartScreen:** On first launch Windows may show a "Windows protected your PC" warning because the app is not yet code-signed. Click **More info → Run anyway**. This is normal for independent open-source tools. See the [FAQ](HELP.md#faq) for more details.

---

## How to Use

### 1. First-run Setup Wizard
On first launch a wizard appears to configure your launch settings:
- **Step 1** — Introduction
- **Step 2** — Select your emulator EXE (e.g. `EmuHawk.exe` for BizHawk)
- **Step 3** — Select the Archipelago Lua connector script (typically `connector_bizhawk_emuHawk.lua` inside your Archipelago install)
- **Step 4** — Select `SNI.exe` and choose a community tracker

You can skip any step and configure them later, or re-run the wizard at any time with the **⚙ Run Setup Wizard…** button.

### 2. Open your Archipelago Patch
Click **Open Patch...** and select your `.aplttp` file. The tool will:
- Extract player name, server address, and game info from the patch
- Generate the `.sfc` ROM automatically from your base ROM (prompted on first use)
- If the `.sfc` already exists, it is reused without re-patching

You can also select a `.sfc` file directly — the tool will look for a matching `.aplttp` alongside it to extract metadata.

### 3. (Optional) Set a Link Sprite
In the sprite header at the top:
- Click **Browse File...** to select a local `.zspr` or `.spr` sprite file
- Click **Browse Sprites...** to open the online sprite browser — search, star favorites, and click **Select Sprite** to download and apply
- Click **✕** to clear the sprite (default Link will be used)

**Random sprite:** the top two cards in the browser are **Random All** (**?**) and **Random Favorites** (**?★**). Selecting either saves your preference — the actual sprite is chosen at apply time as a surprise.

### 4. Assign music to slots

**From your Music Library (recommended):**
On first launch the app creates a `MusicLibrary/` folder next to its executable. Drop your audio files there, then:
1. Click the **Library** button on any track row
2. The popup shows all songs in your library with play buttons
3. Click a song name to assign it to the slot

**Browsing manually:**
- Click **Pick File** (or **Replace**) on any row to open a file picker
- Non-PCM files are converted to MSU-1 PCM automatically

### 5. Save / Load Playlists

Your track assignments can be saved as a **playlist** (`.json`) for reuse:
- Click **Save Playlist** to save your current assignments
- Click **Load Playlist** to restore a saved set of assignments

**Sharing music packs:**
- Click **Export Pack…** to bundle all assigned PCM files into a `.lttppack` archive
- Share with anyone — they click **Import Pack…** to extract and load in one step

### 6. Enhance & Launch
Click **Enhance & Launch**. The tool will:
1. Apply the selected Link sprite to the ROM (in-place)
2. Create the `.msu` marker file (only when music tracks are assigned)
3. Write all assigned `.pcm` files with the correct numbered names
4. Start SNI.exe if not already running
5. Launch the SNI client with your server address prefilled, the community tracker, and the emulator (with Lua connector)

In the SNI client, click **Connect** and enter your slot name when prompted — that's the only manual step.

You can apply with just a sprite, just music, or both.

---

## Pack Export / Import (`.lttppack`)

The `.lttppack` format lets you share a complete music setup with other players.

| What's included | What's NOT included |
|-----------------|---------------------|
| All assigned PCM audio files | Your ROM file |
| Track-to-slot mapping | Any sprite |
| Playlist name | |

**Exporting:** Click **Export Pack…** in the MUSIC header. Choose a destination.

**Importing:** Click **Import Pack…**. PCM files are extracted to `MusicLibrary/Imported/<pack name>/`. Your current assignments are replaced by the pack's assignments.

---

## Supported Audio Formats

| Format | Extensions |
|--------|-----------|
| MSU-1 PCM (direct, no conversion) | `.pcm` |
| MP3 | `.mp3` |
| WAV | `.wav` |
| Windows Media Audio / Video | `.wma`, `.wmv` |
| AAC / MPEG-4 Audio | `.aac`, `.m4a`, `.mp4` |
| AIFF | `.aiff`, `.aif` |

When you pick a non-PCM file, the converted `.pcm` is cached in `MusicLibrary/_cache/`. Conversion uses Windows Media Foundation — no additional software required.

> **Note:** FLAC and OGG/Vorbis are not currently supported. Convert them to MP3 or WAV first using a free tool like [Audacity](https://www.audacityteam.org/) or [fre:ac](https://www.freac.org/).

---

## MSU-1 PCM Format

MSU-1 PCM files use the following layout:

| Bytes | Content |
|-------|---------|
| 0–3 | ASCII signature: `MSU1` |
| 4–7 | Loop point (uint32, little-endian) — `0` = loop from beginning |
| 8+ | Raw PCM: 44,100 Hz · 16-bit signed · stereo interleaved |

---

## Troubleshooting

If the app crashes or fails to start, a crash log is automatically written to:

```
%LocalAppData%\LTTPEnhancementTools\crash.log
```

Settings are stored in:
```
%LocalAppData%\LTTPEnhancementTools\launchSettings.json   ← emulator, SNI, tracker paths
%LocalAppData%\LTTPEnhancementTools\settings.json         ← library folder path
```

Delete `launchSettings.json` to force the setup wizard to reappear on next launch.

Please include the crash log when reporting an issue. For common questions, see [HELP.md](HELP.md).

---

## Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (WPF requires Windows)

### Run in development
```bash
dotnet run
```

### Build self-contained installer EXE
```bash
publish.bat
```
This runs `dotnet publish` (produces a single ~70 MB self-contained EXE) and then compiles the Inno Setup installer if [Inno Setup 6](https://jrsoftware.org/isinfo.php) is installed.

Or publish manually:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# Output: bin\Release\net8.0-windows\win-x64\publish\LTTPEnhancementTools.exe
```

### Project structure
```
LTTPEnhancementTools/
├── Models/
│   ├── TrackSlot.cs              # Per-slot data + validation state
│   ├── ArchipelagoMetadata.cs    # Parsed .aplttp patch metadata
│   ├── Playlist.cs               # Playlist model (name + slot→path map)
│   ├── LibraryEntry.cs           # Music Library song entry model
│   └── SpriteEntry.cs            # Sprite browser entry model
├── Services/
│   ├── ArchipelagoPatchReader.cs # .aplttp ZIP parser
│   ├── AudioPlayer.cs            # NAudio PCM playback
│   ├── PcmConverter.cs           # Audio → MSU-1 PCM conversion
│   ├── PcmValidator.cs           # MSU-1 header validation
│   ├── MsuApplyEngine.cs         # Pack assembly engine
│   ├── SpriteApplier.cs          # ZSPR/SPR sprite patching
│   ├── MusicLibrary.cs           # Library folder scanner + cache manager
│   ├── PlaylistManager.cs        # Save/load JSON playlists
│   ├── PlaylistBundleManager.cs  # Export/import .lttppack archives
│   ├── AutoSaveManager.cs        # Persist last session state
│   ├── FavoritesManager.cs       # Persist sprite favorites
│   ├── AppSettings.cs            # App-level settings (library path)
│   ├── SettingsManager.cs        # Persist AppSettings to %LocalAppData%
│   ├── OriginalSoundtrackManager.cs # Import/match/convert/cache original OST
│   ├── LaunchSettings.cs         # Launch settings (emulator, SNI, tracker)
│   ├── LaunchSettingsManager.cs  # Persist LaunchSettings to %LocalAppData%
│   ├── PreviewCache.cs           # Self-healing preview image cache (atomic writes)
│   ├── SpriteThumbnailRenderer.cs # Render thumbnails from ZSPR pixel data (SNES 4bpp)
│   └── JsonDefaults.cs           # Shared JSON serializer options + HttpClient
├── Converters/
│   └── ValueConverters.cs        # WPF value converters
├── Resources/
│   ├── trackCatalog.json         # Slot number → track name mapping (61 slots)
│   ├── Styles.xaml               # Dark theme resource dictionary
│   └── icon.ico                  # App icon
├── Controls/
│   └── SpriteImageControl.xaml/.cs   # Sprite card thumbnail with triforce loading animation
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── SetupWizardWindow.xaml / SetupWizardWindow.xaml.cs
├── SpriteBrowserWindow.xaml / SpriteBrowserWindow.xaml.cs
├── LTTPEnhancementTools.Tests/   # xUnit test suite (runs in CI on every push)
├── LTTPEnhancementTools.csproj
├── setup.iss                     # Inno Setup installer script
└── publish.bat                   # One-command build + package script
```

### Dependencies
- [NAudio 2.2.1](https://github.com/naudio/NAudio) — audio decoding, resampling, and playback
- Windows Media Foundation (built into Windows) — MP3/AAC/WMA/WMV decoding

---

## Legal & Copyright

**ALttP and all its assets** (music, graphics, sprites, ROM) are the exclusive property of Nintendo Co., Ltd. This project has no affiliation with Nintendo.

**This tool does not include, bundle, or distribute any copyrighted game content**, including but not limited to:
- ROM files
- Game music or audio data
- Sprite or graphics data
- Any other Nintendo-owned intellectual property

**Users are solely responsible** for ensuring they legally own any ROM they modify with this tool, and for complying with all applicable laws regarding backup copies of software they own.

The community sprite repository at [alttpr.com](https://alttpr.com) is maintained independently by the ALttP Randomizer community. Sprites displayed in the browser are the work of their respective creators.

This software is provided as-is for personal, non-commercial use. The authors make no warranties and accept no liability for any damages arising from its use.
