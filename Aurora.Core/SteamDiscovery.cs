using System.Text.RegularExpressions;

namespace Aurora.Core;

// Register trusted Workshop IDs in source, not in untrusted Workshop metadata.
// Discovery checks the presence of files; it never loads or installs unsigned DLLs.
public record RegisteredWorkshopMod(string SteamId, string Name, string[] RequiredFiles);
public record DetectedWorkshopMod(RegisteredWorkshopMod Registration, string Folder, string[] MissingFiles)
{
    public bool Complete => MissingFiles.Length == 0;
}

public static class SteamDiscovery
{
    public static readonly IReadOnlyList<RegisteredWorkshopMod> RegisteredMods =
    [
        new("3822550289", "PBR", ["mods/m.dll", "mods/pbr.dll", "mods/glass_default.dds"])
    ];

    public static List<string> FindWorkshopRoots(string game)
    {
        string fullGame = Path.GetFullPath(game);
        var steamapps = Directory.GetParent(fullGame)?.Parent?.FullName;
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (steamapps != null) libraries.Add(steamapps);
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps"));
        foreach (var apps in libraries.ToArray())
        {
            string vdf = Path.Combine(apps, "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "steamapps"));
        }

        var roots = libraries.Select(x => Path.Combine(x, "workshop", "content", "784150")).ToList();
        // Creator's in-game Workshop workspace (media_soviet/workshop_wip/<Steam ID>/mods).
        roots.Add(Path.Combine(fullGame, "media_soviet", "workshop_wip"));
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindRoots(string game)
    {
        var roots = new List<string> { Path.Combine(game, "mods", "library") };
        roots.AddRange(FindWorkshopRoots(game));
        return roots;
    }

    public static List<DetectedWorkshopMod> FindRegisteredWorkshop(string game)
    {
        var detected = new List<DetectedWorkshopMod>();
        var roots = FindWorkshopRoots(game);
        foreach (var registration in RegisteredMods)
        {
            DetectedWorkshopMod? best = null;
            foreach (var root in roots)
            {
                var folder = Path.Combine(root, registration.SteamId);
                if (!Directory.Exists(folder)) continue;
                try
                {
                    // Skip linked Workshop roots and payloads, and never trust manifest.json
                    // or workshopconfig.ini as executable authorization.
                    var release = WorkshopUpdates.ReadRelease(folder, registration);
                    var expected = release?.Files ?? registration.RequiredFiles;
                    var missing = expected
                        .Where(relative =>
                        {
                            try { return !File.Exists(ModCatalog.SafePath(folder, relative)); }
                            catch (InvalidDataException) { return true; }
                        }).ToArray();
                    var match = new DetectedWorkshopMod(registration, folder, missing);
                    if (best == null || (!best.Complete && match.Complete)) best = match;
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                { /* Unsafe or malformed Workshop item. The update scanner reports details. */ }
            }
            if (best != null) detected.Add(best);
        }
        return detected;
    }
}
