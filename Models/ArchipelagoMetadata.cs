namespace LTTPEnhancementTools.Models;

public record ArchipelagoMetadata(
    string Server,
    int Player,
    string PlayerName,
    string Game,
    string PatchFilePath,
    string ExpectedSfcPath,
    string BaseChecksum
);
