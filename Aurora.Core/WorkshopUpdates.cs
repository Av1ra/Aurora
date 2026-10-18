using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aurora.Core;

public sealed record WorkshopRelease(int Schema, string SteamId, string Name, string Version, string ReleaseDate, string[] Files);
public sealed record WorkshopFileHash(string Path, string Sha256);
public sealed record WorkshopInstalled(string SteamId, string Version, string ReleaseDate, WorkshopFileHash[] Files);
public sealed record WorkshopUpdateOffer(RegisteredWorkshopMod Registration, string Folder, WorkshopRelease Release, WorkshopInstalled? Installed, WorkshopFileHash[] SourceFiles)
{
    public bool FirstInstall => Installed == null;
}

// Steam downloads subscribed Workshop items; Aurora only reads registered local folders.
// aurora-module.json is readable release metadata, not a digital signature.
public static class WorkshopUpdates
{
    public const string MetadataName = "aurora-module.json";
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static string ReceiptPath(string game, string steamId) =>
        ModCatalog.SafePath(game, "mods/.aurora-workshop/" + steamId + ".json");

    static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    public static WorkshopRelease? ReadRelease(string folder, RegisteredWorkshopMod registration)
    {
        var meta = ModCatalog.SafePath(folder, MetadataName);
        if (!File.Exists(meta)) return null;
        if (new FileInfo(meta).Length > 64 * 1024) throw new InvalidDataException("Workshop release description too large");
        var release = JsonSerializer.Deserialize<WorkshopRelease>(File.ReadAllText(meta), JsonOptions)
            ?? throw new InvalidDataException("Invalid Workshop release description");
        if (release.Schema != 1 || release.SteamId != registration.SteamId ||
            !Version.TryParse(release.Version, out var version) || version.Major < 0 ||
            !DateOnly.TryParseExact(release.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            string.IsNullOrWhiteSpace(release.Name) || release.Name.Length > 100 ||
            release.Files is null || release.Files.Length is < 1 or > 100 ||
            release.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != release.Files.Length ||
            release.Files.Any(path => !registration.RequiredFiles.Contains(path, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("Workshop version, date, Steam ID or file list is invalid");
        // A registry entry controls which exact destinations this item is allowed to replace.
        return release;
    }

    public static WorkshopInstalled? ReadInstalled(string game, string steamId)
    {
        string path = ReceiptPath(game, steamId);
        if (!File.Exists(path)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<WorkshopInstalled>(File.ReadAllText(path), JsonOptions);
            if (state?.SteamId != steamId || !Version.TryParse(state.Version, out _) ||
                state.Files is null || state.Files.Any(x => x == null || x.Path == null || x.Sha256 == null))
                return null;
            return state;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException)
        {
            return null;
        }
    }

    static WorkshopFileHash[] SourceHashes(string folder, WorkshopRelease release)
    {
        var hashes = new List<WorkshopFileHash>();
        foreach (var relative in release.Files)
        {
            var file = ModCatalog.SafePath(folder, relative);
            if (!File.Exists(file)) throw new FileNotFoundException("Incomplete Workshop payload", file);
            hashes.Add(new(relative, Hash(file)));
        }
        return hashes.ToArray();
    }

    public static (List<WorkshopUpdateOffer> Offers, List<string> Errors) Scan(string game)
    {
        var offers = new List<WorkshopUpdateOffer>();
        var errors = new List<string>();
        if (!File.Exists(Path.Combine(game, "SOVIET64.exe"))) return (offers, errors);
        foreach (var registration in SteamDiscovery.RegisteredMods)
        {
            WorkshopUpdateOffer? best = null;
            foreach (var root in SteamDiscovery.FindWorkshopRoots(game))
            {
                var folder = Path.Combine(root, registration.SteamId);
                if (!Directory.Exists(folder)) continue;
                try
                {
                    var release = ReadRelease(folder, registration);
                    if (release == null) continue; // Empty Workshop folder: no update notice.
                    var hashes = SourceHashes(folder, release);
                    var installed = ReadInstalled(game, registration.SteamId);
                    if (installed != null)
                    {
                        int relation = Version.Parse(release.Version).CompareTo(Version.Parse(installed.Version));
                        if (relation < 0) continue; // Never offer a downgrade.
                        bool filesMatch = hashes.Length == installed.Files.Length &&
                            hashes.All(x => installed.Files.Any(y => y.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) &&
                                                      y.Sha256.Equals(x.Sha256, StringComparison.OrdinalIgnoreCase)));
                        // Recheck installed files as well, so deleted/tampered payloads can be repaired.
                        if (relation == 0 && filesMatch &&
                            hashes.All(x => File.Exists(ModCatalog.SafePath(game, x.Path)) &&
                                            Hash(ModCatalog.SafePath(game, x.Path)).Equals(x.Sha256, StringComparison.OrdinalIgnoreCase)))
                            continue;
                    }
                    var offer = new WorkshopUpdateOffer(registration, folder, release, installed, hashes);
                    if (best == null || Version.Parse(offer.Release.Version) > Version.Parse(best.Release.Version))
                        best = offer;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
                {
                    errors.Add(registration.SteamId + ": " + ex.Message);
                }
            }
            if (best != null) offers.Add(best);
        }
        return (offers, errors);
    }

    public static void Install(string game, WorkshopUpdateOffer selected)
    {
        if (!File.Exists(Path.Combine(game, "SOVIET64.exe")))
            throw new InvalidDataException("Game folder is missing");

        var registration = selected.Registration;
        // Workshop metadata does not authorize arbitrary deletions: only the
        // source-code allowlist and previously tracked files are eligible.
        var current = ReadRelease(selected.Folder, registration)
            ?? throw new InvalidDataException("Workshop release disappeared");
        var sourceHashes = SourceHashes(selected.Folder, current);
        if (current.Version != selected.Release.Version ||
            current.ReleaseDate != selected.Release.ReleaseDate ||
            sourceHashes.Length != selected.SourceFiles.Length ||
            sourceHashes.Any(x => !selected.SourceFiles.Any(y =>
                y.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) &&
                y.Sha256.Equals(x.Sha256, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Workshop files changed during the update check; refresh the launcher");

        var receipt = ReceiptPath(game, registration.SteamId);
        var installed = ReadInstalled(game, registration.SteamId);
        if (installed != null && Version.Parse(current.Version) < Version.Parse(installed.Version))
            throw new InvalidDataException("Workshop downgrade refused");

        // Never delete unknown game files, even if someone edited the receipt.
        // An old file is retired only when it is in this ID's allowlist, was
        // recorded as installed by Aurora, and still matches the recorded hash.
        var retired = installed?.Files
            .Where(old => registration.RequiredFiles.Contains(old.Path, StringComparer.OrdinalIgnoreCase) &&
                          !sourceHashes.Any(newFile => newFile.Path.Equals(old.Path, StringComparison.OrdinalIgnoreCase)))
            .Where(old => old.Sha256.Length == 64 && old.Sha256.All(Uri.IsHexDigit))
            .Where(old =>
            {
                var destination = ModCatalog.SafePath(game, old.Path);
                return File.Exists(destination) && Hash(destination).Equals(old.Sha256, StringComparison.OrdinalIgnoreCase);
            }).ToArray() ?? [];

        // Transaction files exist only while updating. No lasting backups or
        // duplicate DLLs are kept after a successful installation.
        string transaction = ModCatalog.SafePath(game, "mods/.aurora-workshop/.transaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(transaction);
        var applied = new List<(string Destination, string? PriorFile)>();
        try
        {
            // Prepare the entire new payload before touching the installed one.
            for (int i = 0; i < sourceHashes.Length; i++)
            {
                var item = sourceHashes[i];
                var source = ModCatalog.SafePath(selected.Folder, item.Path);
                var staged = Path.Combine(transaction, "new-" + i.ToString(CultureInfo.InvariantCulture));
                File.Copy(source, staged);
                if (!Hash(staged).Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Workshop file changed while copying: " + item.Path);
            }

            // Snapshot only destinations about to be changed for immediate
            // rollback if Windows reports a locked file or another I/O error.
            int number = 0;
            foreach (var relative in sourceHashes.Select(x => x.Path).Concat(retired.Select(x => x.Path))
                                                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var destination = ModCatalog.SafePath(game, relative);
                if (Directory.Exists(destination)) throw new IOException("Destination is a folder: " + relative);
                string? prior = null;
                if (File.Exists(destination))
                {
                    prior = Path.Combine(transaction, "old-" + (number++).ToString(CultureInfo.InvariantCulture));
                    File.Copy(destination, prior);
                }
                applied.Add((destination, prior));
            }

            foreach (var item in retired)
                File.Delete(ModCatalog.SafePath(game, item.Path));

            for (int i = 0; i < sourceHashes.Length; i++)
            {
                var destination = ModCatalog.SafePath(game, sourceHashes[i].Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(Path.Combine(transaction, "new-" + i.ToString(CultureInfo.InvariantCulture)),
                          destination, true);
            }

            // Receipt is the one persistent ledger, not a copy of the DLLs.
            Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
            string pending = Path.Combine(transaction, "installed.json");
            File.WriteAllText(pending, JsonSerializer.Serialize(
                new WorkshopInstalled(current.SteamId, current.Version, current.ReleaseDate, sourceHashes), JsonOptions));
            File.Move(pending, receipt, true);
        }
        catch
        {
            foreach (var (destination, prior) in applied.AsEnumerable().Reverse())
            {
                if (prior != null) File.Copy(prior, destination, true);
                else if (File.Exists(destination)) File.Delete(destination);
            }
            throw;
        }
        finally
        {
            // Old files are temporary only; no mods/aurora-backups directory.
            Directory.Delete(transaction, recursive: true);
        }
    }
}
