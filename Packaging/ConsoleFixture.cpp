// Manual UI test only. Never included in the launcher package or game mods.
#define WIN32_LEAN_AND_MEAN
#include <filesystem>
#include <string>
#include <windows.h>
namespace fs = std::filesystem;
#ifdef START_HOST
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
  wchar_t file[32768];
  GetModuleFileNameW(nullptr, file, 32768);
  auto folder = fs::path(file).parent_path();
  auto game = folder / L"SOVIET64.exe";
  std::wstring command = L"\"" + game.wstring() + L"\"";
  STARTUPINFOW si{sizeof(si)};
  PROCESS_INFORMATION pi{};
  if (!CreateProcessW(game.c_str(), command.data(), nullptr, nullptr, FALSE, 0,
                      nullptr, folder.c_str(), &si, &pi))
    return 1;
  auto host =
      folder.parent_path() / L"package" / L"app" / L"Aurora.Launcher.exe";
  command = L"\"" + host.wstring() + L"\" --game-console --pid " +
            std::to_wstring(pi.dwProcessId);
  CloseHandle(pi.hThread);
  CloseHandle(pi.hProcess);
  if (!CreateProcessW(host.c_str(), command.data(), nullptr, nullptr, FALSE, 0,
                      nullptr, host.parent_path().c_str(), &si, &pi))
    return 2;
  CloseHandle(pi.hThread);
  CloseHandle(pi.hProcess);
  return 0;
}
#else
static LRESULT CALLBACK Proc(HWND w, UINT m, WPARAM a, LPARAM b) {
  if (m == WM_DESTROY) {
    PostQuitMessage(0);
    return 0;
  }
  return DefWindowProcW(w, m, a, b);
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  WNDCLASSW wc{};
  wc.lpfnWndProc = Proc;
  wc.hInstance = instance;
  wc.lpszClassName = L"AuroraConsoleFixture";
  wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
  RegisterClassW(&wc);
  auto w =
      CreateWindowW(wc.lpszClassName, L"Aurora console test (not the game)",
                    WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 600, 400,
                    nullptr, nullptr, instance, nullptr);
  ShowWindow(w, SW_SHOW);
  MSG m;
  while (GetMessageW(&m, nullptr, 0, 0) > 0) {
    TranslateMessage(&m);
    DispatchMessageW(&m);
  }
  return 0;
}
#endif
