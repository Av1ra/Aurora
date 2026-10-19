// Minimal bootstrap adapted from SovietCDK.cpp, WORKING_LOADER snapshot 2026-08-28.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <tlhelp32.h>
#include <string>
#include <vector>
#include "Common.h"
#include "PbrUiProtocol.h"
#include <shellapi.h>
#include "Sha256.h"
#include "PbrRuntimeDefaults.h"
namespace {
bool X64(const std::wstring& file){
 // Read only the DOS and NT headers, never the whole game executable.
 HANDLE h=CreateFileW(file.c_str(),GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,nullptr);
 if(h==INVALID_HANDLE_VALUE)return false;
 IMAGE_DOS_HEADER dos{};IMAGE_NT_HEADERS64 nt{};DWORD read=0;LARGE_INTEGER size{},offset{};
 bool ok=GetFileSizeEx(h,&size)&&ReadFile(h,&dos,sizeof(dos),&read,nullptr)&&read==sizeof(dos)&&dos.e_magic==IMAGE_DOS_SIGNATURE&&dos.e_lfanew>=0&&static_cast<LONGLONG>(dos.e_lfanew)+sizeof(nt)<=size.QuadPart;
 if(ok){offset.QuadPart=dos.e_lfanew;ok=SetFilePointerEx(h,offset,nullptr,FILE_BEGIN)&&ReadFile(h,&nt,sizeof(nt),&read,nullptr)&&read==sizeof(nt)&&nt.Signature==IMAGE_NT_SIGNATURE&&nt.FileHeader.Machine==IMAGE_FILE_MACHINE_AMD64;}
 CloseHandle(h);return ok;
}
uintptr_t ModuleBase(DWORD pid,const wchar_t* name){
 for(int tries=0;tries<100;++tries){HANDLE s=CreateToolhelp32Snapshot(TH32CS_SNAPMODULE,pid);if(s==INVALID_HANDLE_VALUE){if(GetLastError()==ERROR_BAD_LENGTH){Sleep(5);continue;}return 0;}
  MODULEENTRY32W m{};m.dwSize=sizeof(m);uintptr_t base=0;if(Module32FirstW(s,&m))do{if(!_wcsicmp(m.szModule,name)){base=reinterpret_cast<uintptr_t>(m.modBaseAddr);break;}}while(Module32NextW(s,&m));CloseHandle(s);return base;
 }return 0;
}
bool Inject(HANDLE process,DWORD pid,const std::wstring& dll){
 auto load=GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW");HMODULE owner=nullptr;
 if(!load||!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,reinterpret_cast<LPCWSTR>(load),&owner))return false;
 const auto ownerName=pbr::BaseName(pbr::ModulePath(owner));
 // Before the first thread starts, the loader has not populated the module list.
 // Same-bitness Windows system DLLs share their boot-time mapping; the historical
 // SovietCDK loader uses this address. Verify the mapping after loader startup.
 auto remoteBase=ModuleBase(pid,ownerName.c_str());if(!remoteBase)remoteBase=reinterpret_cast<uintptr_t>(owner);
 auto remoteLoad=reinterpret_cast<LPTHREAD_START_ROUTINE>(remoteBase+reinterpret_cast<uintptr_t>(load)-reinterpret_cast<uintptr_t>(owner));
 SIZE_T size=(dll.size()+1)*sizeof(wchar_t),written=0;void* remote=VirtualAllocEx(process,nullptr,size,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!remote)return false;
 if(!WriteProcessMemory(process,remote,dll.c_str(),size,&written)||written!=size){VirtualFreeEx(process,remote,0,MEM_RELEASE);return false;}
 HANDLE thread=CreateRemoteThread(process,nullptr,0,remoteLoad,remote,0,nullptr);
 if(!thread){VirtualFreeEx(process,remote,0,MEM_RELEASE);return false;}
 DWORD wait=WaitForSingleObject(thread,15000);CloseHandle(thread);
 if(wait!=WAIT_OBJECT_0)return false; // Caller terminates only its own suspended game; input remains valid until then.
 VirtualFreeEx(process,remote,0,MEM_RELEASE);
 return ModuleBase(pid,ownerName.c_str())==remoteBase&&ModuleBase(pid,pbr::BaseName(dll).c_str())!=0; // Verify full 64-bit bases after initialization.
}
int Error(const wchar_t* text){
 if(GetEnvironmentVariableW(L"PBR_BOOTSTRAP_TEST",nullptr,0))pbr::WriteUtf8File(pbr::JoinPath(pbr::DirectoryOf(pbr::ModulePath(nullptr)),L"bootstrap-error.txt"),pbr::WideToUtf8(text),false);
 else MessageBoxW(nullptr,text,L"PBR",MB_OK|MB_ICONERROR);return 1;
}
bool GameRunning(const std::wstring& game){
 HANDLE snapshot=CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS,0);if(snapshot==INVALID_HANDLE_VALUE)return false;
 PROCESSENTRY32W entry{sizeof(entry)};bool found=false;
 if(Process32FirstW(snapshot,&entry))do{if(!_wcsicmp(entry.szExeFile,L"SOVIET64.exe")&&!_wcsicmp(pbr::PbrProcessImage(entry.th32ProcessID).c_str(),game.c_str())){found=true;break;}}while(Process32NextW(snapshot,&entry));
 CloseHandle(snapshot);return found;
}
}
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){
 int argc=0;LPWSTR* argv=CommandLineToArgvW(GetCommandLineW(),&argc);
 if(!argv||argc<2||(argc-2)%2!=0)return Error(L"Invalid Aurora launch request.");
 const std::wstring root=argv[1],game=pbr::JoinPath(root,L"SOVIET64.exe"),mods=pbr::JoinPath(root,L"mods");
 if(!X64(game)||GameRunning(game)){LocalFree(argv);return Error(L"Close the running game and select a valid x64 game installation.");}
 std::vector<std::wstring> libraries;
 for(int i=2;i<argc;i+=2){std::wstring file=argv[i];if(!X64(file)||pbr::Utf8ToWide(pbr::Sha256File(file))!=argv[i+1]){LocalFree(argv);return Error(L"A module changed after verification. Search for mods again.");}libraries.push_back(file);}
 LocalFree(argv);
 SetEnvironmentVariableW(L"SteamAppId",L"784150");SetEnvironmentVariableW(L"SteamGameId",L"784150");
 SetEnvironmentVariableW(L"PBR_GAME_ROOT",root.c_str());SetEnvironmentVariableW(L"PBR_MODS_DIR",mods.c_str());
 const auto ui=pbr::JoinPath(pbr::DirectoryOf(pbr::ModulePath(nullptr)),L"Aurora.Launcher.exe");SetEnvironmentVariableW(L"AURORA_UI_PATH",ui.c_str());
 const auto config=pbr::JoinPath(mods,L"pbr-runtime.ini");
 for(const auto& library:libraries)if(!_wcsicmp(pbr::BaseName(library).c_str(),L"pbr.dll")&&!aurora::EnsurePbrRuntimeDefaults(config))return Error(L"Unable to save PBR runtime defaults. Check the mods folder permissions.");
 SetEnvironmentVariableW(L"PBR_DEBUG",GetPrivateProfileIntW(L"runtime",L"debug",0,config.c_str())?L"1":L"0");
 SetEnvironmentVariableW(L"PBR_CONTROLS",L"1");SetEnvironmentVariableW(L"PBR_CONSOLE",L"0");SetEnvironmentVariableW(L"PBR_MODEL_MODE",L"NATIVE");
 auto eventName=L"Local\\AURORA_PBR_"+std::to_wstring(GetCurrentProcessId());HANDLE ready=CreateEventW(nullptr,TRUE,FALSE,eventName.c_str());SetEnvironmentVariableW(L"PBR_BOOTSTRAP_EVENT",eventName.c_str());
 STARTUPINFOW si{sizeof(si)};PROCESS_INFORMATION pi{};std::wstring command=L"\""+game+L"\"";
 if(!CreateProcessW(game.c_str(),command.data(),nullptr,nullptr,FALSE,CREATE_SUSPENDED|CREATE_UNICODE_ENVIRONMENT,nullptr,root.c_str(),&si,&pi)){CloseHandle(ready);return Error(L"Unable to start game.");}
 bool ok=true;
 for(const auto& library:libraries){
  HANDLE moduleReady=nullptr;const auto name=pbr::BaseName(library);
  if(!_wcsicmp(name.c_str(),L"m.dll")){auto overlayName=L"Local\\M_EARLY_"+std::to_wstring(pi.dwProcessId);moduleReady=CreateEventW(nullptr,TRUE,FALSE,overlayName.c_str());}
  if(!_wcsicmp(name.c_str(),L"pbr.dll"))moduleReady=ready;
  ok=Inject(pi.hProcess,pi.dwProcessId,library)&&(!moduleReady||WaitForSingleObject(moduleReady,15000)==WAIT_OBJECT_0);
  if(moduleReady&&moduleReady!=ready)CloseHandle(moduleReady);if(!ok)break;
 }
 if(!ok){TerminateProcess(pi.hProcess,1);CloseHandle(pi.hThread);CloseHandle(pi.hProcess);CloseHandle(ready);return Error(L"Unable to initialize verified module. The new game process was stopped.");}
 if(ResumeThread(pi.hThread)==DWORD(-1)){TerminateProcess(pi.hProcess,1);CloseHandle(pi.hThread);CloseHandle(pi.hProcess);CloseHandle(ready);return Error(L"Unable to resume game.");}
 const auto pid=std::to_string(pi.dwProcessId);DWORD written=0;WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),pid.data(),static_cast<DWORD>(pid.size()),&written,nullptr);
 CloseHandle(pi.hThread);CloseHandle(pi.hProcess);CloseHandle(ready);return 0;
}
