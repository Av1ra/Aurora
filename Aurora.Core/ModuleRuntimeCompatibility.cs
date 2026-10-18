using System.Security.Cryptography;

namespace Aurora.Core;

public sealed class WorkshopRuntimeConflictException(string steamId, string version)
    : InvalidOperationException("Installed Workshop runtime conflicts with the signed module: " + steamId)
{
    public string SteamId { get; } = steamId;
    public string Version { get; } = version;
}

public static class ModuleRuntimeCompatibility
{
    public static bool OfferMatchesSignedRuntime(WorkshopUpdateOffer offer, IEnumerable<VerifiedMod> modules)
    {
        var files = modules.SelectMany(m => m.Manifest.Files)
            .Where(f => f.Path.StartsWith("Payload/", StringComparison.Ordinal))
            .Select(f => new WorkshopFileHash(f.Path[8..], f.Sha256))
            .Where(f => offer.Registration.RequiredFiles.Contains(f.Path, StringComparer.OrdinalIgnoreCase))
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0 || files.Any(g => g.Select(f => f.Sha256.ToLowerInvariant()).Distinct().Count() != 1)) return false;
        if (!files.Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(offer.SourceFiles.Select(f => f.Path))) return false;
        return files.All(g => offer.SourceFiles.Any(f => f.Path.Equals(g.Key, StringComparison.OrdinalIgnoreCase)
            && f.Sha256.Equals(g.First().Sha256, StringComparison.OrdinalIgnoreCase)));
    }

    // The Workshop updater and signed fallback installer share destinations.
    // Never silently undo a confirmed Workshop update (or restore a retired file)
    // merely because an older signed fallback is still in the launcher bundle.
    public static void EnsureWorkshopCompatible(string game, IEnumerable<VerifiedMod> modules)
    {
        var expected = modules.SelectMany(m => m.Manifest.Files)
            .Where(f => f.Path.StartsWith("Payload/", StringComparison.Ordinal))
            .GroupBy(f => f.Path[8..], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Sha256, StringComparer.OrdinalIgnoreCase);
        foreach (var registration in SteamDiscovery.RegisteredMods)
        {
            var installed = WorkshopUpdates.ReadInstalled(game, registration.SteamId);
            if (installed == null) continue;
            var targets = expected.Where(x => registration.RequiredFiles.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (targets.Length == 0) continue;
            var tracked = installed.Files.Where(x => registration.RequiredFiles.Contains(x.Path, StringComparer.OrdinalIgnoreCase)).ToArray();
            var paths = tracked.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (tracked.Length != paths.Count || !paths.SetEquals(targets.Select(x => x.Key)))
                throw new WorkshopRuntimeConflictException(registration.SteamId, installed.Version);
            foreach (var target in targets)
            {
                var receipt = tracked.Single(x => x.Path.Equals(target.Key, StringComparison.OrdinalIgnoreCase));
                if (!receipt.Sha256.Equals(target.Value, StringComparison.OrdinalIgnoreCase))
                    throw new WorkshopRuntimeConflictException(registration.SteamId, installed.Version);
                var file = ModCatalog.SafePath(game, target.Key);
                if (!File.Exists(file)) continue; // Exact signed bytes may repair a missing file.
                using var input = File.OpenRead(file);
                if (!Convert.ToHexString(SHA256.HashData(input)).Equals(target.Value, StringComparison.OrdinalIgnoreCase))
                    throw new WorkshopRuntimeConflictException(registration.SteamId, installed.Version);
            }
        }
    }
}
