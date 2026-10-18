#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){wchar_t image[32768];GetModuleFileNameW(nullptr,image,32768);std::wstring root=image;root.resize(root.find_last_of(L'\\'));std::wstring exe=root+L"\\Aurora.Launcher.exe",command=L"\""+exe+L"\" --editor";STARTUPINFOW si{sizeof(si)};PROCESS_INFORMATION pi{};if(!CreateProcessW(exe.c_str(),command.data(),nullptr,nullptr,FALSE,0,nullptr,root.c_str(),&si,&pi)){MessageBoxW(nullptr,L"Сначала установите Аврору.",L"Мастер моделей",MB_ICONINFORMATION);return 1;}CloseHandle(pi.hThread);CloseHandle(pi.hProcess);return 0;}
