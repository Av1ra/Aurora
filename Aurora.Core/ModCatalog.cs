using System.Security.Cryptography;
using System.Text.Json;

namespace Aurora.Core;

public record ModFile(string Path, string Sha256);
public record ModOption(string Id, string Label, string Section, string Key, bool Beta = false, bool Default = true,
    string Kind="toggle",int Minimum=0,int Maximum=100,int DefaultValue=0);
public record ModManifest(int Schema, string Id, string Name, string Category, string Version,
    string MinimumLauncher, string Config, ModFile[] Files, ModOption[] Options,string Placement="navigation");
public record SignedEnvelope(string Payload, string Signature);
public record VerifiedMod(string Folder, ModManifest Manifest);

// Metadata is data only. Discovery never calls LoadLibrary/Assembly.Load.
public sealed class ModCatalog(string publicKeyPem)
{
    public static string LauncherVersion => typeof(ModCatalog).Assembly.GetName().Version!.ToString(3);
    public ModManifest Verify(string folder)
    {
        var marker = SafePath(folder, "acode.lnchr");
        if (new FileInfo(marker).Length > 1024 * 1024) throw new InvalidDataException("Manifest too large");
        var envelope = JsonSerializer.Deserialize<SignedEnvelope>(File.ReadAllText(marker)) ?? throw new InvalidDataException("Missing envelope");
        byte[] payload = Convert.FromBase64String(envelope.Payload);
        using var key = ECDsa.Create(); key.ImportFromPem(publicKeyPem);
        if (!key.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256))
            throw new InvalidDataException("Invalid publisher signature");
        var mod = JsonSerializer.Deserialize<ModManifest>(payload) ?? throw new InvalidDataException("Missing manifest");
        if (mod.Schema is not (1 or 2) || !System.Text.RegularExpressions.Regex.IsMatch(mod.Id, @"^[a-z0-9][a-z0-9.-]{1,95}$"))
            throw new InvalidDataException("Unsupported module identity/schema");
        if(!Version.TryParse(mod.Version,out _)) throw new InvalidDataException("Invalid module version");
        if (!Version.TryParse(mod.MinimumLauncher, out var minimum) || minimum > Version.Parse(LauncherVersion))
            throw new InvalidDataException("Launcher update required: " + mod.MinimumLauncher);
        if (mod.Files.Length is < 1 or > 10000 || mod.Options.Length > 64) throw new InvalidDataException("Invalid manifest limits");
        SafePath(folder, mod.Config);
        if(!System.Text.RegularExpressions.Regex.IsMatch(mod.Config,@"^[a-zA-Z0-9_-]+\.ini$")) throw new InvalidDataException("Invalid config filename");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in mod.Files)
        {
            if (!seen.Add(item.Path)) throw new InvalidDataException("Duplicate file");
            var path = SafePath(folder, item.Path);
            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (!hash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("File changed: " + item.Path);
        }
        if(mod.Placement is not ("navigation" or "settings"))throw new InvalidDataException("Invalid module placement");
        foreach(var option in mod.Options)
        {
            if(option.Kind is not ("toggle" or "slider") || (option.Kind=="slider"&&(mod.Schema<2||option.Minimum>=option.Maximum||option.DefaultValue<option.Minimum||option.DefaultValue>option.Maximum)))throw new InvalidDataException("Invalid module control");
            if(new[]{option.Section,option.Key}.Any(x=>!System.Text.RegularExpressions.Regex.IsMatch(x,@"^[a-zA-Z0-9_]{1,64}$")))
                throw new InvalidDataException("Invalid setting key");
        }
        return mod;
    }

    public (List<VerifiedMod> Mods, List<string> Errors) Discover(IEnumerable<string> roots)
    {
        var mods = new List<VerifiedMod>(); var errors = new List<string>();
        foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            var folders = new List<string>{root};
            // Steam content/<appid>/<publishedfileid> and mods/library/<modid>.
            folders.AddRange(Directory.EnumerateDirectories(root).Where(p => (File.GetAttributes(p)&FileAttributes.ReparsePoint)==0));
            foreach(var folder in folders)
            {
                if (!File.Exists(Path.Combine(folder,"acode.lnchr"))) continue;
                try { var manifest=Verify(folder); mods.Add(new(folder,manifest)); }
                catch(Exception ex) { errors.Add(Path.GetFileName(folder)+": "+ex.Message); }
            }
        }
        return (mods.GroupBy(x=>x.Manifest.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(x=>Version.Parse(x.Manifest.Version)).ThenByDescending(x=>mods.IndexOf(x)).First()).OrderBy(x=>x.Manifest.Category).ThenBy(x=>x.Manifest.Name).ToList(),errors);
    }

    public static string SafePath(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':')||relative.Contains('\\')||relative.Split('/').Any(x=>x is "." or ".." or ""))
            throw new InvalidDataException("Unsafe relative path");
        root=Path.GetFullPath(root);
        if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked root rejected");
        string path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escape");
        for(string? p=path;p!=null&&!p.Equals(root,StringComparison.OrdinalIgnoreCase);p=Path.GetDirectoryName(p))
            if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Linked path rejected");
        return path;
    }
}
