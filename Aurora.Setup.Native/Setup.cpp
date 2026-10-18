#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include "Sha256.h"
#include <commctrl.h>
#include <filesystem>
#include <fstream>
#include <regex>
#include <shellapi.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <tlhelp32.h>
#include <vector>
#include <windows.h>
namespace fs = std::filesystem;
static HWND appEdit, gameEdit, statusText, installButton;
static HFONT font;
static bool busy = false;
static bool cliMode = false;
static fs::path chosenApp;
static fs::path Canonical(const fs::path &input) {
  auto path = fs::absolute(input).lexically_normal();
  while (path != path.root_path() && path.filename().empty())
    path = path.parent_path();
  return path;
}
static std::wstring Text(HWND h) {
  int n = GetWindowTextLengthW(h);
  std::wstring s(n + 1, L'\0');
  GetWindowTextW(h, s.data(), n + 1);
  s.resize(n);
  return s;
}
static fs::path Local() {
  PWSTR p = nullptr;
  if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &p)))
    throw std::runtime_error("Local app data unavailable");
  fs::path value = p;
  CoTaskMemFree(p);
  return value / L"Aurora";
}
static std::string Utf8(const std::wstring &s) {
  if (s.empty())
    return {};
  int n = WideCharToMultiByte(CP_UTF8, 0, s.data(), int(s.size()), nullptr, 0,
                              nullptr, nullptr);
  std::string out(n, '\0');
  WideCharToMultiByte(CP_UTF8, 0, s.data(), int(s.size()), out.data(), n,
                      nullptr, nullptr);
  return out;
}
static std::wstring ReadPath(const fs::path &p) {
  std::ifstream in(p, std::ios::binary);
  std::string s;
  std::getline(in, s);
  if (!s.empty() && s.back() == '\r')
    s.pop_back();
  int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(),
                              int(s.size()), nullptr, 0);
  std::wstring out(n, L'\0');
  if (n)
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(), int(s.size()),
                        out.data(), n);
  return out;
}
static void WritePath(const fs::path &p, const fs::path &value) {
  fs::create_directories(p.parent_path());
  std::ofstream out(p, std::ios::binary);
  out << Utf8(value.wstring());
  if (!out)
    throw std::runtime_error("Cannot save installation path");
}
static bool Within(const fs::path &file, const fs::path &parent) {
  auto f = fs::absolute(file).lexically_normal().wstring(),
       p = fs::absolute(parent).lexically_normal().wstring();
  if (p.back() != L'\\')
    p += L'\\';
  return f.size() > p.size() && !_wcsnicmp(f.c_str(), p.c_str(), p.size());
}
static bool Linked(const fs::path &file) {
  DWORD attributes = GetFileAttributesW(file.c_str());
  return attributes != INVALID_FILE_ATTRIBUTES &&
         (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
}
static void Shortcut(const fs::path &app) {
  PWSTR programs = nullptr;
  if (FAILED(SHGetKnownFolderPath(FOLDERID_Programs, 0, nullptr, &programs)))
    return;
  fs::path folder = programs;
  CoTaskMemFree(programs);
  IShellLinkW *link = nullptr;
  if (SUCCEEDED(CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER,
                                 IID_PPV_ARGS(&link)))) {
    link->SetPath((app / L"Aurora Launcher.exe").c_str());
    link->SetWorkingDirectory(app.c_str());
    IPersistFile *persist = nullptr;
    if (SUCCEEDED(link->QueryInterface(IID_PPV_ARGS(&persist)))) {
      persist->Save((folder / L"Aurora Launcher.lnk").c_str(), TRUE);
      persist->Release();
    }
    link->Release();
  }
}
static std::wstring Quote(const std::wstring &s) {
  std::wstring r = L"\"";
  unsigned slash = 0;
  for (wchar_t c : s) {
    if (c == L'\\') {
      ++slash;
      continue;
    }
    if (c == L'\"')
      r.append(slash * 2 + 1, L'\\');
    else
      r.append(slash, L'\\');
    slash = 0;
    r += c;
  }
  r.append(slash * 2, L'\\');
  return r + L"\"";
}
static DWORD Run(const fs::path &executable, std::wstring arguments) {
  std::wstring command = Quote(executable.wstring()) + L" " + arguments;
  STARTUPINFOW si{sizeof(si)};
  si.dwFlags = STARTF_USESHOWWINDOW;
  si.wShowWindow = SW_HIDE;
  PROCESS_INFORMATION pi{};
  if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr,
                      FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
    throw std::runtime_error("Cannot start Windows archive extractor");
  CloseHandle(pi.hThread);
  DWORD wait = WaitForSingleObject(pi.hProcess, 120000);
  DWORD code = 1;
  if (wait == WAIT_OBJECT_0)
    GetExitCodeProcess(pi.hProcess, &code);
  CloseHandle(pi.hProcess);
  if (wait != WAIT_OBJECT_0)
    throw std::runtime_error("Archive extraction timed out");
  return code;
}
static void Start(const fs::path &app) {
  std::wstring cmd = Quote((app / L"Aurora Launcher.exe").wstring());
  STARTUPINFOW si{sizeof(si)};
  PROCESS_INFORMATION pi{};
  if (!CreateProcessW((app / L"Aurora Launcher.exe").c_str(), cmd.data(), nullptr,
                      nullptr, FALSE, 0, nullptr, app.c_str(), &si, &pi))
    throw std::runtime_error("Cannot launch Aurora");
  CloseHandle(pi.hThread);
  CloseHandle(pi.hProcess);
}
static void CheckClosed(const fs::path &target) {
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  if (snapshot == INVALID_HANDLE_VALUE)
    throw std::runtime_error("Cannot check running applications");
  PROCESSENTRY32W entry{sizeof(entry)};
  bool open = false;
  if (Process32FirstW(snapshot, &entry))
    do {
      if (entry.th32ProcessID == GetCurrentProcessId())
        continue;
      if (_wcsicmp(entry.szExeFile, L"Aurora.Launcher.exe") &&
          _wcsicmp(entry.szExeFile, L"Aurora Editor.exe") &&
          _wcsicmp(entry.szExeFile, L"Aurora.ModelBackend.exe"))
        continue;
      HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE,
                                   entry.th32ProcessID);
      if (process) {
        wchar_t image[32768];
        DWORD n = 32768;
        if (QueryFullProcessImageNameW(process, 0, image, &n) &&
            Within(image, target))
          open = true;
        CloseHandle(process);
      }
    } while (Process32NextW(snapshot, &entry));
  CloseHandle(snapshot);
  if (open)
    throw std::runtime_error(
        "Close Aurora and the model editor before updating.");
}
static unsigned U16(const std::vector<unsigned char> &b, size_t at) {
  if (at + 2 > b.size())
    throw std::runtime_error("Truncated ZIP");
  return b[at] | b[at + 1] << 8;
}
static unsigned U32(const std::vector<unsigned char> &b, size_t at) {
  return U16(b, at) | (U16(b, at + 2) << 16);
}
static void ValidateZip(const std::vector<unsigned char> &b) {
  if (b.size() < 22)
    throw std::runtime_error("Invalid ZIP");
  size_t e = b.size() - 22, limit = b.size() > 65557 ? b.size() - 65557 : 0;
  while (U32(b, e) != 0x06054b50) {
    if (e == limit)
      throw std::runtime_error("ZIP directory missing");
    --e;
  }
  unsigned count = U16(b, e + 10);
  size_t at = U32(b, e + 16);
  if (count == 65535 || U16(b, e + 4) || U16(b, e + 6))
    throw std::runtime_error("Unsupported ZIP layout");
  for (unsigned i = 0; i < count; i++) {
    if (U32(b, at) != 0x02014b50)
      throw std::runtime_error("Invalid ZIP directory");
    unsigned len = U16(b, at + 28);
    if (at + 46 + len > b.size())
      throw std::runtime_error("Truncated ZIP path");
    std::string name(reinterpret_cast<const char *>(b.data() + at + 46), len);
    if (name.empty() || name.front() == '/' || name.find('\\') != name.npos ||
        name.find(':') != name.npos || name.find('\0') != name.npos ||
        name.find('\t') != name.npos ||
        ((U32(b, at + 38) >> 16) & 0xf000) == 0xa000)
      throw std::runtime_error("Unsafe ZIP path");
    std::istringstream segments(name);
    std::string segment;
    while (std::getline(segments, segment, '/'))
      if (segment == ".." || segment == "." || segment.empty() ||
          segment.back() == '.' || segment.back() == ' ')
        throw std::runtime_error("Unsafe ZIP component");
    at += 46 + len + U16(b, at + 30) + U16(b, at + 32);
  }
}
static fs::path ExecutableFolder() {
  wchar_t path[32768];
  GetModuleFileNameW(nullptr, path, 32768);
  return fs::path(path).parent_path();
}
static fs::path FindGame() {
  auto saved = ReadPath(Local() / L"game-path.txt");
  if (!saved.empty() && fs::exists(fs::path(saved) / L"SOVIET64.exe"))
    return saved;
  auto nearby = ExecutableFolder();
  if (fs::exists(nearby / L"SOVIET64.exe"))
    return nearby;
  wchar_t steam[32768];
  DWORD bytes = sizeof(steam);
  if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\Valve\\Steam", L"SteamPath",
                   RRF_RT_REG_SZ, nullptr, steam, &bytes) == ERROR_SUCCESS) {
    std::vector<fs::path> libraries{steam};
    std::ifstream in(fs::path(steam) / L"steamapps" / L"libraryfolders.vdf");
    std::string text((std::istreambuf_iterator<char>(in)), {});
    std::regex pattern("\"path\"\\s+\"([^\"]+)\"");
    for (std::sregex_iterator i(text.begin(), text.end(), pattern), end;
         i != end; ++i)
      libraries.push_back(fs::u8path((*i)[1].str()));
    for (const auto &library : libraries) {
      auto game = library / L"steamapps" / L"common" / L"SovietRepublic";
      if (fs::exists(game / L"SOVIET64.exe"))
        return game;
    }
  }
  return {};
}
static fs::path Unpack() {
  HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(102), RT_RCDATA);
  if (!resource)
    throw std::runtime_error(
        "This is an updater. Use Aurora-Setup.exe beside its ZIP archive.");
  auto data =
      static_cast<const char *>(LockResource(LoadResource(nullptr, resource)));
  std::string descriptor(data, data + SizeofResource(nullptr, resource));
  auto split = descriptor.find('\n');
  if (split != 64)
    throw std::runtime_error("Invalid archive descriptor");
  auto name = descriptor.substr(split + 1);
  if (name.find_first_of("/\\:") != name.npos || name.empty())
    throw std::runtime_error("Invalid archive name");
  auto archive = ExecutableFolder() / fs::u8path(name);
  if (pbr::Sha256File(archive.wstring()) != descriptor.substr(0, 64))
    throw std::runtime_error(
        "Place the matching Aurora ZIP archive beside this installer. Archive "
        "SHA-256 verification failed.");
  std::ifstream input(archive, std::ios::binary);
  std::vector<unsigned char> bytes((std::istreambuf_iterator<char>(input)), {});
  ValidateZip(bytes);
  auto folder =
      Local() / L"setup-cache" / std::to_wstring(GetTickCount64()) / L"package";
  fs::create_directories(folder);
  wchar_t system[32768];
  GetSystemDirectoryW(system, 32768);
  if (Run(fs::path(system) / L"tar.exe", L"-xf " + Quote(archive.wstring()) +
                                             L" -C " +
                                             Quote(folder.wstring())) != 0)
    throw std::runtime_error(
        "Windows could not extract the application package");
  return folder;
}
struct Change {
  fs::path file, backup;
  bool existed;
};
static void Install(const fs::path &source, const fs::path &target,
                    bool persist = true) {
  CheckClosed(target);
  if (!fs::exists(source / L"Aurora Launcher.exe") || !fs::exists(source / L"app" / L"Aurora.Launcher.exe"))
    throw std::runtime_error("Incomplete Aurora package");
  if (fs::exists(target) && Linked(target))
    throw std::runtime_error("Linked installation folder is not supported");
  fs::create_directories(target);
  auto backup = Local() / L"backups" / std::to_wstring(GetTickCount64());
  fs::create_directories(backup);
  std::vector<Change> changes;
  try {
    for (const auto &entry : fs::recursive_directory_iterator(source)) {
      if (Linked(entry.path()))
        throw std::runtime_error("Linked payload file rejected");
      if (!entry.is_regular_file())
        continue;
      auto relative = fs::relative(entry.path(), source);
      if (relative == L"app/retired-files.tsv")
        continue;
      auto destination = target / relative;
      if (!Within(destination, target))
        throw std::runtime_error("Unsafe install path");
      for (auto parent = destination.parent_path();
           parent != target && Within(parent, target);
           parent = parent.parent_path())
        if (fs::exists(parent) && Linked(parent))
          throw std::runtime_error("Linked destination rejected");
      fs::create_directories(destination.parent_path());
      bool existed = fs::exists(destination);
      auto previous = backup / relative;
      if (existed) {
        fs::create_directories(previous.parent_path());
        fs::copy_file(destination, previous);
      }
      auto staged = destination;
      staged += L".aurora-new-" + std::to_wstring(GetCurrentProcessId()) +
                L"-" + std::to_wstring(GetTickCount64());
      fs::copy_file(entry.path(), staged, fs::copy_options::overwrite_existing);
      if (!MoveFileExW(staged.c_str(), destination.c_str(),
                       MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        fs::remove(staged);
        throw std::runtime_error(
            "Cannot replace an application file. Close other Aurora windows.");
      }
      changes.push_back({destination, previous, existed});
    }
    // Retire only exact old release files. Unknown/user-modified resources
    // remain.
    std::ifstream retired(source / L"app" / L"retired-files.tsv");
    std::string line;
    while (std::getline(retired, line)) {
      auto tab = line.find('\t');
      if (tab == line.npos)
        continue;
      auto relative = fs::u8path(line.substr(0, tab));
      if (fs::exists(source / relative))
        continue;
      if (fs::exists(target / L"app" / L"Editor" / L"acode.lnchr") &&
          relative.begin() != relative.end() && *relative.begin() == L"Editor")
        continue;
      auto file = target / relative;
      bool linkedParent = false;
      for (auto parent = file.parent_path();
           parent != target && Within(parent, target);
           parent = parent.parent_path())
        if (fs::exists(parent) && Linked(parent))
          linkedParent = true;
      if (linkedParent)
        continue;
      if (!Within(file, target) || !fs::is_regular_file(file) || Linked(file))
        continue;
      std::string hash = line.substr(tab + 1);
      if (!hash.empty() && hash.back() == '\r')
        hash.pop_back();
      if (pbr::Sha256File(file.wstring()) != hash)
        continue;
      auto saved = backup / L"retired" / relative;
      fs::create_directories(saved.parent_path());
      if (!MoveFileExW(file.c_str(), saved.c_str(),
                       MOVEFILE_COPY_ALLOWED | MOVEFILE_WRITE_THROUGH))
        throw std::runtime_error("Cannot archive an old application file");
      changes.push_back({file, saved, true});
    }
    std::ofstream receipt(backup / L"receipt.tsv", std::ios::binary);
    for (const auto &change : changes)
      receipt << Utf8(change.file.wstring()) << '\t'
              << (change.existed ? Utf8(change.backup.wstring()) : "-") << '\n';
    if (persist)
      WritePath(Local() / L"install-root.txt", Canonical(target));
  } catch (...) {
    for (auto i = changes.rbegin(); i != changes.rend(); ++i) {
      std::error_code error;
      if (i->existed) {
        fs::create_directories(i->file.parent_path(), error);
        fs::copy_file(i->backup, i->file, fs::copy_options::overwrite_existing,
                      error);
      } else
        fs::remove(i->file, error);
    }
    throw;
  }
}
// The official Steam entry is preserved verbatim as .exe_orig, exactly once.
// Store only a hash of our installed proxy so future updates cannot overwrite
// an unknown or Steam-restored game executable.
static void InstallSteamEntry(const fs::path &application, const fs::path &game) {
  if (!fs::exists(game / L"SOVIET64.exe"))
    throw std::runtime_error("The selected SovietRepublic game folder is invalid");
  if (Linked(game))
    throw std::runtime_error("Linked game folder is not supported");
  fs::path entry = game / L"SETUPAPPLICATION SOVIET.exe";
  fs::path original = game / L"SETUPAPPLICATION SOVIET.exe_orig";
  fs::path proxy = application / L"app" / L"Aurora.Steam.exe";
  fs::path marker = application / L"steam-entry.sha256";
  if (!fs::is_regular_file(proxy) || Linked(proxy))
    throw std::runtime_error("Steam launcher bridge is missing from the Aurora package");
  if (Linked(entry) || Linked(original))
    throw std::runtime_error("Linked Steam entry is not supported");
  if (!fs::exists(entry))
    throw std::runtime_error("SETUPAPPLICATION SOVIET.exe not found in the game folder");

  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  if (snapshot == INVALID_HANDLE_VALUE)
    throw std::runtime_error("Cannot check active game processes");
  PROCESSENTRY32W process{sizeof(process)};
  bool active = false;
  if (Process32FirstW(snapshot, &process))
    do {
      if (process.th32ProcessID == GetCurrentProcessId())
        continue;
      if (!_wcsicmp(process.szExeFile, L"SOVIET64.exe") ||
          !_wcsicmp(process.szExeFile, L"SETUPAPPLICATION SOVIET.exe"))
        active = true;
    } while (Process32NextW(snapshot, &process));
  CloseHandle(snapshot);
  if (active)
    throw std::runtime_error("Close Soviet Republic and its Steam launcher first");

  const std::string nextHash = pbr::Sha256File(proxy.wstring());
  bool hasOriginal = fs::exists(original);
  const std::string currentHash = pbr::Sha256File(entry.wstring());
  if (hasOriginal) {
    std::wstring previous = ReadPath(marker);
    if (currentHash != nextHash &&
        (previous.empty() || currentHash != Utf8(previous)))
      throw std::runtime_error(
          "Steam replaced or changed its game entry. The original _orig file "
          "is preserved. Restore the Steam entry before retrying Aurora.");
  }

  auto staged = entry;
  staged += L".aurora-new-" + std::to_wstring(GetCurrentProcessId());
  auto previous = entry;
  previous += L".aurora-old-" + std::to_wstring(GetCurrentProcessId());
  bool renamedOriginal = false;
  bool savedPrevious = false;
  try {
    fs::copy_file(proxy, staged, fs::copy_options::overwrite_existing);
    if (pbr::Sha256File(staged.wstring()) != nextHash)
      throw std::runtime_error("Steam launcher bridge failed integrity check");
    if (!hasOriginal) {
      // No overwrite flag: never replace the saved official executable.
      if (!MoveFileExW(entry.c_str(), original.c_str(), MOVEFILE_WRITE_THROUGH))
        throw std::runtime_error("Cannot preserve original Steam executable");
      renamedOriginal = true;
    } else {
      fs::copy_file(entry, previous, fs::copy_options::overwrite_existing);
      savedPrevious = true;
    }
    if (!MoveFileExW(staged.c_str(), entry.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
      throw std::runtime_error("Cannot install Steam launcher bridge");
    WritePath(marker, fs::u8path(nextHash));
  } catch (...) {
    std::error_code ignored;
    if (renamedOriginal) {
      fs::remove(entry, ignored);
      MoveFileExW(original.c_str(), entry.c_str(), MOVEFILE_WRITE_THROUGH);
    } else if (savedPrevious) {
      fs::copy_file(previous, entry, fs::copy_options::overwrite_existing,
                    ignored);
    }
    fs::remove(staged, ignored);
    fs::remove(previous, ignored);
    throw;
  }
  std::error_code ignored;
  fs::remove(staged, ignored);
  fs::remove(previous, ignored);
}

// Restore is explicit. Do not touch the official executable if Steam has
// already replaced our bridge or somebody modified it independently.
static void RestoreSteamEntry(const fs::path &application,
                             const fs::path &game) {
  fs::path entry = game / L"SETUPAPPLICATION SOVIET.exe";
  fs::path original = game / L"SETUPAPPLICATION SOVIET.exe_orig";
  fs::path marker = application / L"steam-entry.sha256";
  if (!fs::is_regular_file(original) || !fs::is_regular_file(entry))
    throw std::runtime_error("No managed Steam entry to restore");
  auto hash = ReadPath(marker);
  if (hash.empty() || pbr::Sha256File(entry.wstring()) != Utf8(hash))
    throw std::runtime_error("Refusing to replace an unknown Steam entry");
  auto temporary = entry;
  temporary += L".aurora-restore-" + std::to_wstring(GetCurrentProcessId());
  if (!MoveFileExW(entry.c_str(), temporary.c_str(), MOVEFILE_WRITE_THROUGH))
    throw std::runtime_error("Close Steam and the game before restoring");
  if (!MoveFileExW(original.c_str(), entry.c_str(), MOVEFILE_WRITE_THROUGH)) {
    MoveFileExW(temporary.c_str(), entry.c_str(), MOVEFILE_WRITE_THROUGH);
    throw std::runtime_error("Cannot restore original Steam executable");
  }
  std::error_code ignored;
  fs::remove(temporary, ignored);
  fs::remove(marker, ignored);
}
static std::wstring ChooseFolder(HWND parent) {
  IFileDialog *dialog = nullptr;
  std::wstring value;
  if (SUCCEEDED(CoCreateInstance(CLSID_FileOpenDialog, nullptr,
                                 CLSCTX_INPROC_SERVER,
                                 IID_PPV_ARGS(&dialog)))) {
    DWORD options;
    dialog->GetOptions(&options);
    dialog->SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
    if (SUCCEEDED(dialog->Show(parent))) {
      IShellItem *item = nullptr;
      if (SUCCEEDED(dialog->GetResult(&item))) {
        PWSTR path = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) {
          value = path;
          CoTaskMemFree(path);
        }
        item->Release();
      }
    }
    dialog->Release();
  }
  return value;
}
// Browsing selects a parent folder. Aurora always gets its own subfolder.
static fs::path LauncherFolder(const fs::path &selected) {
  auto folder = Canonical(selected);
  if (_wcsicmp(folder.filename().c_str(), L"Aurora") == 0)
    return folder;
  return folder / L"Aurora";
}
static HWND Control(HWND parent, const wchar_t *type, const wchar_t *text,
                    int x, int y, int width, int height, int id,
                    DWORD style = 0) {
  auto h = CreateWindowExW(
      type == std::wstring(L"EDIT") ? WS_EX_CLIENTEDGE : 0, type, text,
      WS_CHILD | WS_VISIBLE | style, x, y, width, height, parent,
      reinterpret_cast<HMENU>(INT_PTR(id)), nullptr, nullptr);
  SendMessageW(h, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
  return h;
}
static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM w,
                                   LPARAM l) {
  try {
    switch (message) {
    case WM_CREATE: {
      font =
          CreateFontW(-16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                      DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                      CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
      Control(window, L"STATIC", L"Aurora Launcher", 24, 20, 570, 30, 0);
      Control(window, L"STATIC",
              L"Выберите, где хранить приложение и для какой игры искать моды.",
              24, 57, 580, 36, 0);
      Control(window, L"STATIC", L"Папка лаунчера", 24, 104, 500, 24, 0);
      auto detected = FindGame();
      auto initial = detected.empty() ? ReadPath(Local() / L"install-root.txt")
                                      : LauncherFolder(detected).wstring();
      appEdit = Control(window, L"EDIT", initial.c_str(), 24, 132, 485, 28, 10,
                        ES_AUTOHSCROLL | WS_TABSTOP);
      Control(window, L"BUTTON", L"Обзор…", 520, 132, 90, 28, 11, WS_TABSTOP);
      Control(window, L"STATIC", L"Папка игры SovietRepublic", 24, 178, 520, 24,
              0);
      auto game = detected.wstring();
      gameEdit = Control(window, L"EDIT", game.c_str(), 24, 206, 485, 28, 12,
                         ES_AUTOHSCROLL | WS_TABSTOP);
      Control(window, L"BUTTON", L"Обзор…", 520, 206, 90, 28, 13, WS_TABSTOP);
      Control(window, L"STATIC",
              L"Файлы модов будут устанавливаться в папку mods выбранной "
              L"игры.\nAurora Editor устанавливается вместе с лаунчером.",
              24, 250, 585, 48, 0);
      statusText = Control(window, L"STATIC", L"", 24, 312, 580, 42, 0);
      installButton = Control(window, L"BUTTON", L"Установить", 460, 370, 150,
                              36, 14, WS_TABSTOP | BS_DEFPUSHBUTTON);
      Control(window, L"BUTTON", L"Отмена", 340, 370, 105, 36, 15, WS_TABSTOP);
      return 0;
    }
    case WM_COMMAND: {
      int id = LOWORD(w);
      if (busy)
        return 0;
      if (id == 11 || id == 13) {
        auto path = ChooseFolder(window);
        if (!path.empty()) {
          if (id == 13)
            SetWindowTextW(gameEdit, path.c_str());
          SetWindowTextW(appEdit, LauncherFolder(path).c_str());
        }
      }
      if (id == 15)
        DestroyWindow(window);
      if (id == 14) {
        auto app = Text(appEdit), game = Text(gameEdit);
        if (app.empty() || !fs::path(app).is_absolute()) {
          SetWindowTextW(statusText, L"Укажите полный путь к папке лаунчера.");
          return 0;
        }
        if (!game.empty() && !fs::exists(fs::path(game) / L"SOVIET64.exe")) {
          SetWindowTextW(statusText, L"В папке игры не найден SOVIET64.exe.");
          return 0;
        }
        chosenApp = app;
        busy = true;
        EnableWindow(installButton, FALSE);
        SetWindowTextW(statusText, L"Установка приложения…");
        std::thread([window, app, game] {
          std::wstring error;
          try {
            Install(Unpack(), app);
            if (!game.empty()) {
              WritePath(Local() / L"game-path.txt", game);
              InstallSteamEntry(app, game);
            }
            CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            Shortcut(app);
            CoUninitialize();
          } catch (const std::exception &ex) {
            int n = MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, nullptr, 0);
            error.resize(n);
            MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, error.data(), n);
            if (!error.empty())
              error.pop_back();
          }
          PostMessageW(window, WM_APP + 1, 0,
                       reinterpret_cast<LPARAM>(new std::wstring(error)));
        }).detach();
      }
      return 0;
    }
    case WM_APP + 1: {
      auto error = reinterpret_cast<std::wstring *>(l);
      busy = false;
      EnableWindow(installButton, TRUE);
      if (error->empty()) {
        delete error;
        try {
          Start(chosenApp);
        } catch (...) {
          SetWindowTextW(
              statusText,
              L"Приложение установлено. Откройте Аврору из выбранной папки.");
          return 0;
        }
        DestroyWindow(window);
      } else {
        SetWindowTextW(statusText, error->c_str());
        delete error;
      }
      return 0;
    }
    case WM_CLOSE:
      if (!busy)
        DestroyWindow(window);
      return 0;
    case WM_DESTROY:
      DeleteObject(font);
      PostQuitMessage(0);
      return 0;
    }
    return DefWindowProcW(window, message, w, l);
  } catch (...) {
    if (statusText)
      SetWindowTextW(statusText, L"Не удалось открыть выбранный путь.");
    return 0;
  }
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  try {
    int count = 0;
    auto args = CommandLineToArgvW(GetCommandLineW(), &count);
    if (count == 2 && !wcscmp(args[1], L"--test-launcher-folder")) {
      cliMode = true;
      const fs::path parent = L"C:\\Aurora-tests\\SovietRepublic";
      const fs::path expected = parent / L"Aurora";
      const bool pass =
          LauncherFolder(parent) == expected &&
          LauncherFolder(fs::path(parent.wstring() + L"\\")) == expected &&
          LauncherFolder(expected) == expected &&
          LauncherFolder(parent / L"aUrOrA") == parent / L"aUrOrA";
      LocalFree(args);
      CoUninitialize();
      return pass ? 0 : 1;
    }
    if (count == 2 && !wcscmp(args[1], L"--unpack-test")) {
      cliMode = true;
      Unpack();
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count >= 3 && !wcscmp(args[1], L"--validate-zip")) {
      cliMode = true;
      std::ifstream in(fs::path(args[2]), std::ios::binary);
      std::vector<unsigned char> bytes((std::istreambuf_iterator<char>(in)),
                                       {});
      ValidateZip(bytes);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count == 2 && !wcscmp(args[1], L"--restore-steam")) {
      cliMode = true;
      auto game = ReadPath(Local() / L"game-path.txt");
      auto app = ReadPath(Local() / L"install-root.txt");
      if (game.empty() || app.empty())
        throw std::runtime_error("No registered Aurora Steam installation");
      RestoreSteamEntry(app, game);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count == 4 && !wcscmp(args[1], L"--steam-hook-test")) {
      cliMode = true;
      InstallSteamEntry(args[2], args[3]);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count == 4 && !wcscmp(args[1], L"--steam-restore-test")) {
      cliMode = true;
      RestoreSteamEntry(args[2], args[3]);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count >= 3 && !wcscmp(args[1], L"--install-test")) {
      cliMode = true;
      if (count != 4)
        throw std::runtime_error("Invalid test arguments");
      Install(args[2], args[3], false);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    if (count == 4 && !wcscmp(args[1], L"--replace")) {
      fs::path target = Canonical(args[2]);
      auto known = ReadPath(Local() / L"install-root.txt");
      if (known.empty())
        known = (Local() / L"app").wstring();
      if (_wcsicmp(target.c_str(), Canonical(known).c_str()))
        throw std::runtime_error(
            "Update target is not the selected Aurora installation");
      HANDLE parent =
          OpenProcess(SYNCHRONIZE, FALSE, wcstoul(args[3], nullptr, 10));
      if (parent) {
        DWORD waited = WaitForSingleObject(parent, 30000);
        CloseHandle(parent);
        if (waited != WAIT_OBJECT_0)
          throw std::runtime_error("Close Aurora before updating");
      }
      wchar_t self[32768];
      GetModuleFileNameW(nullptr, self, 32768);
      auto source = fs::path(self).parent_path();
      if (!fs::exists(source / L"Aurora Launcher.exe"))
        source = source.parent_path();
      Install(source, target);
      auto game = ReadPath(Local() / L"game-path.txt");
      if (!game.empty() && fs::exists(fs::path(game) / L"SOVIET64.exe") &&
          fs::exists(fs::path(game) / L"SETUPAPPLICATION SOVIET.exe_orig"))
        InstallSteamEntry(target, game);
      Start(target);
      LocalFree(args);
      CoUninitialize();
      return 0;
    }
    LocalFree(args);
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_STANDARD_CLASSES};
    InitCommonControlsEx(&controls);
    WNDCLASSW cls{};
    cls.hInstance = instance;
    cls.lpfnWndProc = WindowProc;
    cls.lpszClassName = L"AuroraInstaller";
    cls.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    cls.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassW(&cls);
    auto window = CreateWindowExW(
        0, cls.lpszClassName, L"Установка Aurora Launcher и Aurora Editor",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, CW_USEDEFAULT,
        CW_USEDEFAULT, 650, 465, nullptr, nullptr, instance, nullptr);
    ShowWindow(window, SW_SHOW);
    MSG message;
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
      if (!IsDialogMessageW(window, &message)) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
      }
  } catch (const std::exception &error) {
    if (!cliMode)
      MessageBoxA(nullptr, error.what(), "Aurora", MB_ICONERROR);
    CoUninitialize();
    return 1;
  }
  CoUninitialize();
  return 0;
}

