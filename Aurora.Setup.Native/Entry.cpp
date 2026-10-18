#ifdef AURORA_STEAM_ENTRY
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <filesystem>
#include <fstream>
#include <string>
#include <iterator>

namespace fs = std::filesystem;

static std::wstring ReadUtf8Path(const fs::path &path) {
  std::ifstream file(path, std::ios::binary);
  if (!file) return {};
  std::string value((std::istreambuf_iterator<char>(file)), {});
  if (auto end = value.find_first_of("\r\n"); end != value.npos) value.resize(end);
  if (value.empty()) return {};
  int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
                                   value.data(), int(value.size()), nullptr, 0);
  if (length <= 0) return {};
  std::wstring wide(length, L'\0');
  if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(),
                          int(value.size()), wide.data(), length) != length)
    return {};
  return wide;
}

// This small Steam entry lives in SovietRepublic, not inside the Aurora folder.
// Keep it alive while Aurora is open so Steam can track the launched process.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR args, int) {
  wchar_t executablePath[32768];
  if (!GetModuleFileNameW(nullptr, executablePath, 32768))
    return 1;
  const auto game = fs::path(executablePath).parent_path();
  fs::path host = game / L"Aurora" / L"app" / L"Aurora.Launcher.exe";
  if (!fs::exists(host)) {
    wchar_t local[32768];
    DWORD n = GetEnvironmentVariableW(L"LOCALAPPDATA", local, 32768);
    if (n > 0 && n < 32768) {
      std::wstring installed = ReadUtf8Path(fs::path(local) / L"Aurora" / L"install-root.txt");
      std::wstring selectedGame = ReadUtf8Path(fs::path(local) / L"Aurora" / L"game-path.txt");
      if (!installed.empty() && !selectedGame.empty() &&
          _wcsicmp(fs::path(selectedGame).lexically_normal().c_str(),
                   game.lexically_normal().c_str()) == 0)
        host = fs::path(installed) / L"app" / L"Aurora.Launcher.exe";
    }
  }
  bool useOriginal = !fs::exists(host);
  if (useOriginal) {
    host = game / L"SETUPAPPLICATION SOVIET.exe_orig";
    if (!fs::exists(host)) {
      MessageBoxW(nullptr,
                  L"Aurora Launcher is missing and the original game entry "
                  L"could not be found. Reinstall Aurora or restore the game.",
                  L"Aurora Launcher", MB_OK | MB_ICONERROR);
      return 1;
    }
  }
  std::wstring command = L"\"" + host.wstring() + L"\"";
  if (useOriginal && args && *args) {
    command += L" ";
    command += args;
  }
  STARTUPINFOW si{sizeof(si)};
  PROCESS_INFORMATION pi{};
  if (!CreateProcessW(host.c_str(), command.data(), nullptr, nullptr, FALSE,
                      0, nullptr,
                      (useOriginal ? game : host.parent_path()).c_str(),
                      &si, &pi)) {
    MessageBoxW(nullptr, L"Failed to start Aurora Launcher or the original game entry.",
                L"Aurora Launcher", MB_OK | MB_ICONERROR);
    return 1;
  }
  CloseHandle(pi.hThread);
  DWORD wait = WaitForSingleObject(pi.hProcess, INFINITE);
  DWORD code = 1;
  if (wait == WAIT_OBJECT_0) GetExitCodeProcess(pi.hProcess, &code);
  CloseHandle(pi.hProcess);
  return code;
}
#else
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
  command += L" --editor";
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
                L"Keep the app folder beside Aurora Launcher.exe and Aurora Editor.exe.",
                L"Aurora", MB_ICONINFORMATION);
    return 1;
  }
  CloseHandle(pi.hThread);
  CloseHandle(pi.hProcess);
  return 0;
}

#endif
