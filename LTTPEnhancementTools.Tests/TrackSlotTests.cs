using System.ComponentModel;
using LTTPEnhancementTools.Models;

namespace LTTPEnhancementTools.Tests;

public class TrackSlotTests
{
    // ── TypeLabel / HasTypeLabel ─────────────────────────────────────────

    [Theory]
    [InlineData("music", "", false)]
    [InlineData("jingle", "[SFX]", true)]
    [InlineData("extended", "[EXT]", true)]
    public void TypeLabel_ReturnsCorrectValues(string trackType, string expectedLabel, bool expectedHasLabel)
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test", TrackType = trackType };

        Assert.Equal(expectedLabel, slot.TypeLabel);
        Assert.Equal(expectedHasLabel, slot.HasTypeLabel);
    }

    // ── PlayButtonText ──────────────────────────────────────────────────

    [Fact]
    public void PlayButtonText_DefaultIsPlay()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        Assert.Equal("\u25B6", slot.PlayButtonText); // ▶
    }

    [Fact]
    public void PlayButtonText_TogglesOnIsPlaying()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };

        slot.IsPlaying = true;
        Assert.Equal("\u25A0", slot.PlayButtonText); // ■

        slot.IsPlaying = false;
        Assert.Equal("\u25B6", slot.PlayButtonText); // ▶
    }

    // ── OriginalPlayButtonText ──────────────────────────────────────────

    [Fact]
    public void OriginalPlayButtonText_DefaultIsNote()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        Assert.Equal("\u266A", slot.OriginalPlayButtonText); // ♪
    }

    [Fact]
    public void OriginalPlayButtonText_TogglesOnIsPlayingOriginal()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };

        slot.IsPlayingOriginal = true;
        Assert.Equal("\u25A0", slot.OriginalPlayButtonText); // ■

        slot.IsPlayingOriginal = false;
        Assert.Equal("\u266A", slot.OriginalPlayButtonText); // ♪
    }

    // ── HasFile / HasOriginal ───────────────────────────────────────────

    [Fact]
    public void HasFile_FalseByDefault()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        Assert.False(slot.HasFile);
        Assert.Null(slot.FileName);
    }

    [Fact]
    public void HasFile_TrueWhenPcmPathSet()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        slot.PcmPath = @"C:\audio\test.pcm";

        Assert.True(slot.HasFile);
        Assert.Equal("test.pcm", slot.FileName);
    }

    [Fact]
    public void HasOriginal_FalseByDefault()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        Assert.False(slot.HasOriginal);
    }

    [Fact]
    public void HasOriginal_TrueWhenOriginalPcmPathSet()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        slot.OriginalPcmPath = @"C:\cache\01.pcm";

        Assert.True(slot.HasOriginal);
    }

    // ── SlotDisplay ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "01")]
    [InlineData(9, "09")]
    [InlineData(10, "10")]
    [InlineData(61, "61")]
    public void SlotDisplay_FormatsWithLeadingZero(int slotNumber, string expected)
    {
        var slot = new TrackSlot { SlotNumber = slotNumber, Name = "Test" };
        Assert.Equal(expected, slot.SlotDisplay);
    }

    // ── PropertyChanged notifications ───────────────────────────────────

    [Fact]
    public void PcmPath_FiresPropertyChanged_ForRelatedProperties()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        var changed = new List<string>();
        slot.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        slot.PcmPath = @"C:\audio\test.pcm";

        Assert.Contains("PcmPath", changed);
        Assert.Contains("FileName", changed);
        Assert.Contains("HasFile", changed);
    }

    [Fact]
    public void OriginalPcmPath_FiresPropertyChanged_ForHasOriginal()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        var changed = new List<string>();
        slot.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        slot.OriginalPcmPath = @"C:\cache\01.pcm";

        Assert.Contains("OriginalPcmPath", changed);
        Assert.Contains("HasOriginal", changed);
    }

    [Fact]
    public void IsPlaying_FiresPropertyChanged_ForPlayButtonText()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        var changed = new List<string>();
        slot.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        slot.IsPlaying = true;

        Assert.Contains("IsPlaying", changed);
        Assert.Contains("PlayButtonText", changed);
    }

    [Fact]
    public void SettingSameValue_DoesNotFirePropertyChanged()
    {
        var slot = new TrackSlot { SlotNumber = 1, Name = "Test" };
        slot.PcmPath = @"C:\audio\test.pcm";

        var changed = new List<string>();
        slot.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        slot.PcmPath = @"C:\audio\test.pcm"; // same value

        Assert.Empty(changed);
    }
}
