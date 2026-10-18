using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
namespace Aurora.Installer;
static class Program
{
 [STAThread]static void Main(string[] args)
 {
  ApplicationConfiguration.Initialize();
  try
  {
   if(args.Length==3&&args[0]=="--replace")
   {
    string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","app");
    string target=Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
    if(!target.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Update target is not the managed Aurora installation");
    try{using var process=Process.GetProcessById(int.Parse(args[2]));if(!process.WaitForExit(30000))throw new IOException("Close Aurora before updating");}catch(ArgumentException){}
    Install(AppContext.BaseDirectory,target);Start(target);return;
   }
   Application.Run(new InstallWindow());
  }
  catch(Exception ex){MessageBox.Show(ex.Message,"Аврора",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 public static void Start(string target)=>Process.Start(new ProcessStartInfo(Path.Combine(target,"Aurora.Launcher.exe")){UseShellExecute=true,WorkingDirectory=target});
 public static void Install(string source,string target)
 {
  Directory.CreateDirectory(target);
  string backup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","backups",DateTime.UtcNow.ToString("yyyyMMddHHmmss")+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
  var changed=new List<(string Path,string? Backup)>();
  try
  {
   foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
   {
    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked files not supported");
    var relative=Path.GetRelativePath(source,file);if(relative=="Aurora.Installer.exe")continue;
    var destination=Path.GetFullPath(Path.Combine(target,relative));if(!destination.StartsWith(target.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe install path");
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    var previous=File.Exists(destination)?Path.Combine(backup,changed.Count+".bak"):null;if(previous!=null)File.Copy(destination,previous);changed.Add((destination,previous));File.Copy(file,destination,true);
   }
   File.WriteAllText(Path.Combine(backup,"receipt.json"),JsonSerializer.Serialize(changed.Select(x=>new{x.Path,x.Backup})));
  }
  catch{foreach(var item in changed.AsEnumerable().Reverse()){if(item.Backup!=null)File.Copy(item.Backup,item.Path,true);else if(File.Exists(item.Path))File.Delete(item.Path);}throw;}
 }
}
sealed class InstallWindow:Form
{
 public InstallWindow()
 {
  Text="Установка Авроры";Font=new Font("Segoe UI",10);Width=540;Height=250;StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
  var label=new Label{Text="Аврора\nЛаунчер модов для Soviet Republic",AutoSize=true,Location=new Point(28,25),Font=new Font("Segoe UI",16)};Controls.Add(label);
  var install=new Button{Text="Установить",Width=170,Height=38,Location=new Point(325,150)};Controls.Add(install);
  var details=new Label{Text="Установка для текущего пользователя. Права администратора не нужны.",AutoSize=false,Width=470,Height=45,Location=new Point(28,95)};Controls.Add(details);
  install.Click+=(_,_)=>
  {
   install.Enabled=false;
   try
   {
    using var embedded=Assembly.GetExecutingAssembly().GetManifestResourceStream("Aurora.Payload")??throw new IOException("This updater has no embedded installer payload. Download Aurora-Setup.exe.");
    string temp=Path.Combine(Path.GetTempPath(),"Aurora-Setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
    using(var zip=new ZipArchive(embedded,ZipArchiveMode.Read))zip.ExtractToDirectory(temp);
    string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","app");Program.Install(temp,target);
    string programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);string shortcut=Path.Combine(programs,"Аврора.url");File.WriteAllText(shortcut,"[InternetShortcut]\r\nURL="+new Uri(Path.Combine(target,"Aurora.Launcher.exe")).AbsoluteUri+"\r\nIconFile="+Path.Combine(target,"Aurora.Launcher.exe")+"\r\nIconIndex=0\r\n");
    Program.Start(target);Close();
   }
   catch(Exception ex){MessageBox.Show(ex.Message,"Аврора",MessageBoxButtons.OK,MessageBoxIcon.Error);install.Enabled=true;}
  };
 }
}
