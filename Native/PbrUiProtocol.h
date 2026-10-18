#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>

namespace pbr {
inline constexpr wchar_t kPbrUiClass[]=L"PBRDebugControlsWindow";
inline constexpr UINT kPbrConsoleGet=WM_APP+0x3a0;
inline constexpr UINT kPbrConsoleSet=WM_APP+0x3a1;
inline UINT PbrUiShowMessage(){return RegisterWindowMessageW(L"PBR.MinimalRuntime.ShowControls.v1");}
inline std::wstring PbrProcessImage(DWORD pid){
 HANDLE process=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);if(!process)return {};
 wchar_t path[32768]{};DWORD size=32768;bool ok=QueryFullProcessImageNameW(process,0,path,&size)!=FALSE;CloseHandle(process);return ok?std::wstring(path,size):std::wstring{};
}
// Select the UI belonging to this exact adjacent game, never another running copy.
inline HWND PbrFindUi(const std::wstring& game){
 struct Search{const std::wstring* game;HWND result;};Search search{&game,nullptr};
 EnumWindows([](HWND w,LPARAM p)->BOOL{
  auto& s=*reinterpret_cast<Search*>(p);wchar_t cls[128]{};GetClassNameW(w,cls,128);if(wcscmp(cls,kPbrUiClass))return TRUE;
  DWORD pid=0;GetWindowThreadProcessId(w,&pid);if(_wcsicmp(PbrProcessImage(pid).c_str(),s.game->c_str()))return TRUE;s.result=w;return FALSE;
 },reinterpret_cast<LPARAM>(&search));return search.result;
}
}
