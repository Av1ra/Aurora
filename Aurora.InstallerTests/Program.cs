namespace Aurora.Installer;
static class InstallerTest
{
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-install-tests-"+Guid.NewGuid().ToString("N")),source=Path.Combine(root,"source"),target=Path.Combine(root,"app");Directory.CreateDirectory(source);Directory.CreateDirectory(target);
  foreach(var name in new[]{"a.txt","z.txt"}){File.WriteAllText(Path.Combine(source,name),"new-"+name);File.WriteAllText(Path.Combine(target,name),"old-"+name);}
  using(var locked=File.Open(Path.Combine(target,"z.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
  {bool rejected=false;try{Program.Install(source,target);}catch(IOException){rejected=true;}Check(rejected,"locked destination fails safely");}
  foreach(var name in new[]{"a.txt","z.txt"})Check(File.ReadAllText(Path.Combine(target,name))=="old-"+name,"rollback restores "+name);
  Check(InstallationLocation.Normalize(target)==Path.GetFullPath(target),"custom install directory is accepted");
  bool rootRejected=false;try{InstallationLocation.Normalize(Path.GetPathRoot(target)!);}catch(IOException){rootRejected=true;}
  Check(rootRejected,"installing into disk root is rejected");
  Program.Install(source,target);foreach(var name in new[]{"a.txt","z.txt"})Check(File.ReadAllText(Path.Combine(target,name))=="new-"+name,"successful atomic replacement "+name);
  var marker=DateTime.UtcNow.AddDays(-2);
  File.SetLastWriteTimeUtc(Path.Combine(target,"a.txt"),marker);
  var before=File.GetLastWriteTimeUtc(Path.Combine(target,"a.txt"));
  Program.Install(source,target);
  Check(File.GetLastWriteTimeUtc(Path.Combine(target,"a.txt"))==before,"identical files are not rewritten on reinstall");
  string live=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","app");
  if(System.Diagnostics.Process.GetProcessesByName("Aurora.Launcher").Any(p=>p.MainModule?.FileName?.StartsWith(live,StringComparison.OrdinalIgnoreCase)==true))
  {bool rejected=false;try{Program.Install(source,live);}catch(IOException ex){rejected=ex.Message.Contains("Закройте");}Check(rejected,"running installed instance rejected before writes");}
  Console.WriteLine("AURORA_INSTALLER_TESTS_PASS");
 }
}
