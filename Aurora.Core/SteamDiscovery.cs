using System.Text.RegularExpressions;
namespace Aurora.Core;
public static class SteamDiscovery
{
    public static List<string> FindRoots(string game)
    {
        var roots=new List<string>{Path.Combine(game,"mods","library")};
        var steamapps=Directory.GetParent(Path.GetFullPath(game))?.Parent?.FullName;
        var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(steamapps!=null) libraries.Add(steamapps);
        var steam=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)+@"\Steam\steamapps";
        libraries.Add(steam);
        foreach(var apps in libraries.ToArray())
        {
            var vdf=Path.Combine(apps,"libraryfolders.vdf");
            if(!File.Exists(vdf))continue;
            foreach(Match match in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(Path.Combine(match.Groups[1].Value.Replace(@"\\",@"\"),"steamapps"));
        }
        roots.AddRange(libraries.Select(x=>Path.Combine(x,"workshop","content","784150")));
        return roots;
    }
}
