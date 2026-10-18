using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
namespace Aurora.Launcher;

// This host only watches the newly launched game's foreground window; no global keyboard hook.
sealed class GameConsoleHost
{
 readonly int gamePid;
 readonly Process game;
 readonly AlphaConsole console=new();
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(35)};
 bool wasDown,visible;
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern nint GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(nint window,out uint pid);
 public GameConsoleHost(int pid)
 {
  game=Process.GetProcessById(pid);gamePid=pid;
  if(!game.ProcessName.Equals("SOVIET64",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected game process");
  console.AppWindow.Closing+=(_,e)=>{e.Cancel=true;console.AppWindow.Hide();visible=false;};
  timer.Tick+=(_,_)=>Tick();timer.Start();
 }
 void Tick()
 {
  if(game.HasExited){timer.Stop();game.Dispose();Application.Current.Exit();return;}
  bool down=(GetAsyncKeyState(0xc0)&0x8000)!=0;
  GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);
  if(down&&!wasDown&&(foreground==gamePid||(visible&&foreground==Environment.ProcessId)))
  {
   if(visible){console.AppWindow.Hide();visible=false;}else{console.Activate();visible=true;}
  }
  wasDown=down;
 }
}
