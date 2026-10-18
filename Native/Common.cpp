#include "Common.h"
#include <cctype>
#include <cstdio>
#include <cwctype>

namespace pbr {

std::wstring ModulePath(HMODULE module) {
    std::wstring buffer(32768, L'\0');
    const DWORD n = ::GetModuleFileNameW(module, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (!n || n >= buffer.size()) return {};
    buffer.resize(n);
    return buffer;
}

std::wstring DirectoryOf(const std::wstring& path) {
    const auto p = path.find_last_of(L"\\/");
    return p == std::wstring::npos ? std::wstring{} : path.substr(0, p);
}

std::wstring JoinPath(const std::wstring& a, const std::wstring& b) {
    if (a.empty()) return b;
    if (b.empty()) return a;
    if ((b.size() >= 2 && b[1] == L':') ||
        (b.size() >= 2 && b[0] == L'\\' && b[1] == L'\\') ||
        b[0] == L'\\' || b[0] == L'/') return b;
    if (a.back() == L'\\' || a.back() == L'/') return a + b;
    return a + L"\\" + b;
}

std::wstring FullPath(const std::wstring& path) {
    if (path.empty()) return {};
    DWORD n = ::GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
    if (!n) return path;
    std::wstring out(n, L'\0');
    DWORD w = ::GetFullPathNameW(path.c_str(), n, out.data(), nullptr);
    if (!w || w >= n) return path;
    out.resize(w);
    return out;
}

bool FileExists(const std::wstring& path) {
    const DWORD a = ::GetFileAttributesW(path.c_str());
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

bool DirectoryExists(const std::wstring& path) {
    const DWORD a = ::GetFileAttributesW(path.c_str());
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY) != 0;
}

bool EnsureDirectory(const std::wstring& path) {
    if (path.empty()) return false;
    if (DirectoryExists(path)) return true;
    const std::wstring parent = DirectoryOf(path);
    if (!parent.empty() && parent != path && !DirectoryExists(parent) && !EnsureDirectory(parent)) return false;
    if (::CreateDirectoryW(path.c_str(), nullptr)) return true;
    return ::GetLastError() == ERROR_ALREADY_EXISTS;
}

std::wstring Utf8ToWide(const std::string& text) {
    if (text.empty()) return {};
    int n = ::MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), nullptr, 0);
    UINT cp = CP_UTF8;
    DWORD flags = MB_ERR_INVALID_CHARS;
    if (n <= 0) {
        cp = CP_ACP;
        flags = 0;
        n = ::MultiByteToWideChar(cp, flags, text.data(), static_cast<int>(text.size()), nullptr, 0);
    }
    if (n <= 0) return {};
    std::wstring out(static_cast<std::size_t>(n), L'\0');
    ::MultiByteToWideChar(cp, flags, text.data(), static_cast<int>(text.size()), out.data(), n);
    return out;
}

std::string WideToUtf8(const std::wstring& text) {
    if (text.empty()) return {};
    const int n = ::WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
    if (n <= 0) return {};
    std::string out(static_cast<std::size_t>(n), '\0');
    ::WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), out.data(), n, nullptr, nullptr);
    return out;
}

std::wstring BaseName(const std::wstring& path) {
    const auto p = path.find_last_of(L"\\/");
    return p == std::wstring::npos ? path : path.substr(p + 1);
}

std::wstring TrimWide(const std::wstring& value) {
    std::size_t b = 0;
    while (b < value.size() && iswspace(value[b])) ++b;
    std::size_t e = value.size();
    while (e > b && iswspace(value[e - 1])) --e;
    return value.substr(b, e - b);
}

std::string TrimAscii(const std::string& value) {
    std::size_t b = 0;
    while (b < value.size() && std::isspace(static_cast<unsigned char>(value[b]))) ++b;
    std::size_t e = value.size();
    while (e > b && std::isspace(static_cast<unsigned char>(value[e - 1]))) --e;
    return value.substr(b, e - b);
}

std::string LowerAscii(std::string value) {
    for (char& c : value) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return value;
}

std::vector<std::string> SplitLines(const std::string& text) {
    std::vector<std::string> out;
    std::size_t pos = 0;
    while (pos <= text.size()) {
        std::size_t e = text.find('\n', pos);
        if (e == std::string::npos) e = text.size();
        std::string line = text.substr(pos, e - pos);
        if (!line.empty() && line.back() == '\r') line.pop_back();
        out.push_back(std::move(line));
        if (e == text.size()) break;
        pos = e + 1;
    }
    return out;
}

bool ReadFileBytes(const std::wstring& path, std::string* out) {
    if (!out) return false;
    out->clear();
    FILE* f = nullptr;
    _wfopen_s(&f, path.c_str(), L"rb");
    if (!f) return false;
    char buffer[8192];
    while (true) {
        const std::size_t n = std::fread(buffer, 1, sizeof(buffer), f);
        if (n) out->append(buffer, n);
        if (n < sizeof(buffer)) break;
    }
    const bool ok = !std::ferror(f);
    std::fclose(f);
    return ok;
}

bool WriteUtf8File(const std::wstring& path, const std::string& text, bool append) {
    FILE* f = nullptr;
    _wfopen_s(&f, path.c_str(), append ? L"ab" : L"wb");
    if (!f) return false;
    const std::size_t w = std::fwrite(text.data(), 1, text.size(), f);
    std::fflush(f);
    std::fclose(f);
    return w == text.size();
}

}  // namespace pbr
