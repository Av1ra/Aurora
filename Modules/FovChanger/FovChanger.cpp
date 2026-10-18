#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <filesystem>
#include <fstream>
#include "Resolver.h"
#include "Sha256.h"
namespace fs=std::filesystem;
static HMODULE self;
static void Status(const char* text){wchar_t dll[32768];GetModuleFileNameW(self,dll,32768);std::ofstream(fs::path(dll).parent_path()/L"fov-status.txt")<<text;}
static DWORD WINAPI Start(void*){
 wchar_t image[32768];GetModuleFileNameW(nullptr,image,32768);fs::path exe=image;
 if(_wcsicmp(exe.filename().c_str(),L"SOVIET64.exe")||pbr::Sha256File(image)!="296644a9f207d609031fc2ae73fed2dcb34619a1d55a35d1c7b51965ce6841b8"){Status("Unsupported executable. FOV was not changed.");return 0;}
 auto ini=exe.parent_path()/L"mods"/L"fov.ini";
 if(!GetPrivateProfileIntW(L"camera",L"enabled",1,ini.c_str())){Status("Disabled.");return 0;}
 int degrees=GetPrivateProfileIntW(L"camera",L"degrees",45,ini.c_str());if(degrees<20||degrees>100){Status("Invalid setting. FOV was not changed.");return 0;}
 auto base=reinterpret_cast<unsigned char*>(GetModuleHandleW(nullptr));auto dos=reinterpret_cast<IMAGE_DOS_HEADER*>(base);auto nt=reinterpret_cast<IMAGE_NT_HEADERS64*>(base+dos->e_lfanew);
 auto section=IMAGE_FIRST_SECTION(nt);float* target=nullptr;
 for(unsigned i=0;i<nt->FileHeader.NumberOfSections;++i){auto& s=section[i];if(!(s.Characteristics&IMAGE_SCN_MEM_READ)||(s.Characteristics&(IMAGE_SCN_CNT_CODE|IMAGE_SCN_MEM_WRITE)))continue;
  size_t at=aurora::fov::FindTable(base+s.VirtualAddress,s.Misc.VirtualSize);if(at==aurora::fov::missing)continue;if(target){Status("Ambiguous camera table. FOV was not changed.");return 0;}target=reinterpret_cast<float*>(base+s.VirtualAddress+at);
 }
 if(!target){Status("Camera table not found. FOV was not changed.");return 0;}
 DWORD old=0;if(!VirtualProtect(target,sizeof(float),PAGE_READWRITE,&old)){Status("Camera constant could not be updated.");return 0;}
 *target=degrees*0.01745329251994329577f;DWORD ignored=0;VirtualProtect(target,sizeof(float),old,&ignored);
 Status("FOV applied. Settings take effect on the next game launch.");return 0;
}
BOOL WINAPI DllMain(HMODULE module,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){self=module;DisableThreadLibraryCalls(module);HANDLE worker=CreateThread(nullptr,0,Start,nullptr,0,nullptr);if(worker)CloseHandle(worker);}return TRUE;}
