using LTTPEnhancementTools.Models;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class OriginalSoundtrackManagerTests
{
    /// <summary>
    /// Builds the full 61-slot track list matching trackCatalog.json.
    /// </summary>
    private static List<TrackSlot> BuildTracks()
    {
        var jingles = new HashSet<int> { 1, 8, 10, 12, 19, 25, 26, 29, 30, 32 };
        var entries = new (int slot, string name)[]
        {
            (1, "Opening"), (2, "Light World"), (3, "Rainy Intro"), (4, "Bunny Theme"),
            (5, "Lost Woods"), (6, "Prologue"), (7, "Kakariko"), (8, "Portal Sound"),
            (9, "Dark World"), (10, "Pedestal Pull"), (11, "File / Game Over"),
            (12, "Guards Appear"), (13, "Dark Death Mtn."), (14, "Minigame"),
            (15, "Skull Woods Overworld"), (16, "Hyrule Castle"), (17, "Pendant Dungeon"),
            (18, "Cave"), (19, "Boss Victory"), (20, "Sanctuary"), (21, "Boss Battle"),
            (22, "Crystal Dungeon"), (23, "Shop"), (24, "Cave (duplicate)"),
            (25, "Zelda Rescued"), (26, "Crystal Retrieved"), (27, "Fairy"),
            (28, "Agahnim Floor"), (29, "Ganon Reveal"), (30, "Ganon Dropdown"),
            (31, "Ganon Battle"), (32, "Triforce"), (33, "Epilogue"), (34, "Credits"),
            (35, "Eastern Palace"), (36, "Desert Palace"), (37, "Agahnim's Tower"),
            (38, "Swamp Palace"), (39, "Palace of Darkness"), (40, "Misery Mire"),
            (41, "Skull Woods"), (42, "Ice Palace"), (43, "Tower of Hera"),
            (44, "Thieves' Town"), (45, "Turtle Rock"), (46, "Ganon's Tower"),
            (47, "Armos Knights"), (48, "Lanmolas"), (49, "Agahnim 1"),
            (50, "Arrghus"), (51, "Helmasaur King"), (52, "Vitreous"),
            (53, "Mothula"), (54, "Kholdstare"), (55, "Moldorm"),
            (56, "Blind"), (57, "Trinexx"), (58, "Agahnim 2"),
            (59, "Ganon's Tower Ascent"), (60, "Light World 2"), (61, "Dark World 2"),
        };

        return entries.Select(e => new TrackSlot
        {
            SlotNumber = e.slot,
            Name = e.name,
            TrackType = e.slot >= 35 ? "extended" : jingles.Contains(e.slot) ? "jingle" : "music"
        }).ToList();
    }

    // ── Pass 1: OST alias matching ──────────────────────────────────────

    [Fact]
    public void AliasMatch_HyruleFieldMainTheme_MapsToLightWorld()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\01. Hyrule Field Main Theme.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(2), "Should map to slot 2 (Light World) via alias, not slot 1");
        Assert.False(result.ContainsKey(1), "Should NOT map to slot 1 based on leading number");
    }

    [Fact]
    public void AliasMatch_DarkGoldenLand_MapsToDarkWorld()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\Dark Golden Land.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.Equal(9, result.Keys.Single());
    }

    [Fact]
    public void AliasMatch_SillyPinkRabbit_MapsToBunnyTheme()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\04. Silly Pink Rabbit.flac" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(4));
    }

    [Fact]
    public void AliasMatch_ApostropheHandling_ZeldasRescue()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\Princess Zelda's Rescue.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(25), "Should match slot 25 despite apostrophe");
    }

    [Fact]
    public void AliasMatch_SmartQuoteApostrophe_ZeldasRescue()
    {
        var tracks = BuildTracks();
        // Unicode right single quotation mark (U+2019)
        var files = new List<string> { "C:\\music\\Princess Zelda\u2019s Rescue.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(25), "Should match slot 25 despite smart quote apostrophe");
    }

    // ── Pass 2: Leading number matching ─────────────────────────────────

    [Fact]
    public void LeadingNumber_02_MapsToSlot2()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\02 - Light World.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(2));
    }

    [Fact]
    public void LeadingNumber_34_MapsToSlot34()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\34.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(34));
    }

    [Fact]
    public void LeadingNumber_OutOfRange_NoMatch()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\00.mp3", @"C:\music\62.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.Empty(result);
    }

    // ── Pass 3: Number anywhere ─────────────────────────────────────────

    [Fact]
    public void AnyNumber_Track07_MapsToSlot7()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\Track_07_Kakariko.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(7));
    }

    // ── Pass 4: Fuzzy name matching ─────────────────────────────────────

    [Fact]
    public void FuzzyName_LostWoods_MapsToSlot5()
    {
        var tracks = BuildTracks();
        // Use a filename that won't match alias or number
        var files = new List<string> { @"C:\music\Lost Woods.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(5));
    }

    [Fact]
    public void FuzzyName_GanonBattle_MapsToSlot31()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\Ganon Battle.wav" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(31));
    }

    // ── Priority / conflict tests ───────────────────────────────────────

    [Fact]
    public void AliasTakesPrecedenceOverNumber_InternetArchiveScenario()
    {
        var tracks = BuildTracks();
        // Internet Archive: "10. Hyrule Field Main Theme.mp3" — alias should win over leading "10"
        var files = new List<string> { @"C:\music\10. Hyrule Field Main Theme.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(2), "Alias 'Hyrule Field Main Theme' should map to slot 2");
        Assert.False(result.ContainsKey(10), "Leading number 10 should NOT be used when alias matches");
    }

    [Fact]
    public void SameSlotNotAssignedTwice()
    {
        var tracks = BuildTracks();
        // Both files could match slot 2 via alias
        var files = new List<string>
        {
            @"C:\music\Hyrule Field.mp3",
            @"C:\music\Overworld.mp3"
        };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        // Only one should be assigned to slot 2
        Assert.True(result.ContainsKey(2));
        Assert.Single(result.Where(kv => kv.Key == 2));
    }

    [Fact]
    public void UnmatchedFiles_AreSkipped()
    {
        var tracks = BuildTracks();
        var files = new List<string> { @"C:\music\completely_random_name_xyz.mp3" };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyFileList_ReturnsEmpty()
    {
        var tracks = BuildTracks();
        var files = new List<string>();

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.Empty(result);
    }

    [Fact]
    public void MultipleFiles_MatchCorrectly()
    {
        var tracks = BuildTracks();
        var files = new List<string>
        {
            @"C:\music\01. Title.mp3",          // alias → slot 1
            @"C:\music\02 - Light World.mp3",    // number → slot 2
            @"C:\music\Dark Golden Land.mp3",    // alias → slot 9
            @"C:\music\Lost Woods.mp3",          // alias → slot 5
        };

        var result = OriginalSoundtrackManager.MatchFilesToSlots(files, tracks);

        Assert.True(result.ContainsKey(1));
        Assert.True(result.ContainsKey(2));
        Assert.True(result.ContainsKey(9));
        Assert.True(result.ContainsKey(5));
        Assert.Equal(4, result.Count);
    }
}
