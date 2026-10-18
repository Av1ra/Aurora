namespace Aurora.Launcher;
static class InstallationPaths
{
 public static string Root => Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) is { } parent && Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Equals("app",StringComparison.OrdinalIgnoreCase) ? parent.FullName : AppContext.BaseDirectory;
 public static string SuggestedGame => Directory.GetParent(Root.TrimEnd(Path.DirectorySeparatorChar))?.FullName??"";
}
