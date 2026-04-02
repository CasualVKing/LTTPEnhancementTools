using System.IO;

namespace LTTPEnhancementTools.Services;

public record ApplyRequest(
    string RomSourcePath,
    string OutputDir,
    IReadOnlyDictionary<string, string> Tracks, // slot -> pcm path
    OverwriteMode OverwriteMode,
    string? OutputBaseName = null,     // overrides ROM filename stem for all output files
    string? SpriteSourcePath = null,   // optional .zspr/.spr to inject into the output ROM
    bool InPlace = false               // skip ROM copy; apply sprite directly to source ROM
);

public enum OverwriteMode { Ask, Overwrite, Skip }

public record ApplyConflict(string FileName, string DestPath);

public record ApplySuccess(IReadOnlyList<string> FilesWritten);

public class ApplyEngine
{
    /// <summary>
    /// Raised when conflicts are detected and OverwriteMode is Ask.
    /// The caller must set e.Resolution before the task can continue.
    /// </summary>
    public event EventHandler<ConflictsDetectedEventArgs>? ConflictsDetected;

    public async Task<ApplySuccess> RunAsync(
        ApplyRequest req,
        IProgress<(string step, int current, int total)> progress,
        CancellationToken ct = default)
    {
        var sortedSlots = req.Tracks
            .OrderBy(kv => int.TryParse(kv.Key, out int n) ? n : int.MaxValue)
            .ToList();

        bool hasSprite = !string.IsNullOrEmpty(req.SpriteSourcePath);
        int totalSteps = 4 + sortedSlots.Count + (hasSprite ? 1 : 0); // validate, mkdir, rom, [sprite], msu, pcms

        // STEP 1 — Validate
        progress.Report(("Validating inputs...", 0, totalSteps));
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(req.RomSourcePath))
            throw new FileNotFoundException($"ROM file not found: {req.RomSourcePath}");

        foreach (var (slot, pcmPath) in sortedSlots)
        {
            if (!File.Exists(pcmPath))
                throw new FileNotFoundException($"PCM file for slot {slot} not found: {pcmPath}");
        }

        if (hasSprite)
        {
            if (!File.Exists(req.SpriteSourcePath))
                throw new FileNotFoundException($"Sprite file not found: {req.SpriteSourcePath}");

            var spriteValidationError = SpriteApplier.Validate(req.SpriteSourcePath!);
            if (spriteValidationError != null)
                throw new InvalidDataException($"Invalid sprite file: {spriteValidationError}");
        }

        // STEP 2 — Compute output names
        string romExt = Path.GetExtension(req.RomSourcePath);
        string baseName = !string.IsNullOrWhiteSpace(req.OutputBaseName)
            ? req.OutputBaseName.Trim()
            : Path.GetFileNameWithoutExtension(req.RomSourcePath);
        string romDest = Path.Combine(req.OutputDir, baseName + romExt);
        string msuDest = Path.Combine(req.OutputDir, baseName + ".msu");
        var pcmDests = sortedSlots
            .Select(kv => (slot: kv.Key, src: kv.Value,
                           dest: Path.Combine(req.OutputDir, $"{baseName}-{kv.Key}.pcm")))
            .ToList();

        // STEP 3 — Conflict detection (only prompt for PCM files; ROM + .msu always overwrite)
        progress.Report(("Checking for conflicts...", 1, totalSteps));
        var pcmConflicts = pcmDests
            .Where(p => File.Exists(p.dest))
            .Select(p => new ApplyConflict(Path.GetFileName(p.dest), p.dest))
            .ToList();

        OverwriteMode effectiveMode = req.OverwriteMode;
        if (pcmConflicts.Count > 0 && req.OverwriteMode == OverwriteMode.Ask)
        {
            var args = new ConflictsDetectedEventArgs(pcmConflicts);
            ConflictsDetected?.Invoke(this, args);
            await args.ResolutionTask; // wait for UI to respond
            effectiveMode = args.Resolution;

            if (effectiveMode == OverwriteMode.Ask) // user cancelled
                throw new OperationCanceledException("Apply cancelled by user.");
        }

        var skipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (effectiveMode == OverwriteMode.Skip)
        {
            foreach (var c in pcmConflicts) skipPaths.Add(c.DestPath);
        }

        // STEP 4 — Create directory
        progress.Report(("Creating output directory...", 2, totalSteps));
        Directory.CreateDirectory(req.OutputDir);

        var filesWritten = new List<string>();

        // STEP 5 — Copy ROM (skip when applying in-place)
        if (req.InPlace)
        {
            progress.Report(("Using ROM in-place...", 3, totalSteps));
            romDest = req.RomSourcePath;
        }
        else
        {
            progress.Report(("Copying ROM...", 3, totalSteps));
            await Task.Run(() => File.Copy(req.RomSourcePath, romDest, overwrite: true), ct);
            filesWritten.Add(romDest);
        }

        // STEP 6 (optional) — Apply sprite to output ROM
        int msuStepIndex = 4;
        if (hasSprite)
        {
            progress.Report(("Applying sprite...", 4, totalSteps));
            var spriteError = await Task.Run(() => SpriteApplier.Apply(req.SpriteSourcePath!, romDest), ct);
            if (spriteError != null)
                throw new InvalidOperationException($"Sprite injection failed: {spriteError}");
            msuStepIndex = 5;
        }

        // STEP 7 — Write 0-byte .msu only when music tracks are assigned
        if (sortedSlots.Count > 0)
        {
            progress.Report(("Writing .msu marker...", msuStepIndex, totalSteps));
            await Task.Run(() => File.WriteAllBytes(msuDest, Array.Empty<byte>()), ct);
            filesWritten.Add(msuDest);
        }

        // STEP 8 — Copy PCMs
        for (int i = 0; i < pcmDests.Count; i++)
        {
            var (slot, src, dest) = pcmDests[i];
            progress.Report(($"Copying track {slot}...", msuStepIndex + 1 + i, totalSteps));
            ct.ThrowIfCancellationRequested();

            if (!skipPaths.Contains(dest))
            {
                await Task.Run(() => File.Copy(src, dest, overwrite: effectiveMode == OverwriteMode.Overwrite), ct);
                filesWritten.Add(dest);
            }
        }

        progress.Report(("Done.", totalSteps, totalSteps));
        return new ApplySuccess(filesWritten);
    }
}

public class ConflictsDetectedEventArgs : EventArgs
{
    public IReadOnlyList<ApplyConflict> Conflicts { get; }
    public OverwriteMode Resolution { get; set; } = OverwriteMode.Ask; // Ask = cancelled

    private readonly TaskCompletionSource _tcs = new();
    public Task ResolutionTask => _tcs.Task;

    public ConflictsDetectedEventArgs(IReadOnlyList<ApplyConflict> conflicts)
        => Conflicts = conflicts;

    public void Complete() => _tcs.TrySetResult();
}
