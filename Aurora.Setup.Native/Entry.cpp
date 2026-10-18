#define WIN32_LEAN_AND_MEAN
#include <string>
#include <windows.h>
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR args, int) {
  wchar_t image[32768];
  GetModuleFileNameW(nullptr, image, 32768);
  std::wstring root = image;
  root.resize(root.find_last_of(L'\\'));
  std::wstring executable = root + L"\\app\\Aurora.Launcher.exe",
               command = L"\"" + executable + L"\"";
#ifdef AURORA_EDITOR_ENTRY
  command += L" --open-editor";
#endif
  // Forward only application-owned arguments used by the console host and
  // editor.
  if (args && *args) {
    command += L" ";
    command += args;
  }
  STARTUPINFOW si{sizeof(si)};
  PROCESS_INFORMATION pi{};
  if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr,
                      FALSE, 0, nullptr, (root + L"\\app").c_str(), &si, &pi)) {
    MessageBoxW(nullptr,
                L"Keep the app folder beside Aurora.exe and Aurora.Editor.exe.",
                L"Aurora", MB_ICONINFORMATION);
    return 1;
  }
  CloseHandle(pi.hThread);
  CloseHandle(pi.hProcess);
  return 0;
}
