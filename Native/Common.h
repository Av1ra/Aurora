#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
#include <vector>

namespace pbr {
std::wstring ModulePath(HMODULE module);
std::wstring DirectoryOf(const std::wstring& path);
std::wstring JoinPath(const std::wstring& a, const std::wstring& b);
std::wstring FullPath(const std::wstring& path);
bool FileExists(const std::wstring& path);
bool DirectoryExists(const std::wstring& path);
bool EnsureDirectory(const std::wstring& path);
std::wstring Utf8ToWide(const std::string& text);
std::string WideToUtf8(const std::wstring& text);
std::wstring BaseName(const std::wstring& path);
std::wstring TrimWide(const std::wstring& value);
std::string TrimAscii(const std::string& value);
std::string LowerAscii(std::string value);
std::vector<std::string> SplitLines(const std::string& text);
bool ReadFileBytes(const std::wstring& path, std::string* out);
bool WriteUtf8File(const std::wstring& path, const std::string& text, bool append);
}  // namespace pbr
