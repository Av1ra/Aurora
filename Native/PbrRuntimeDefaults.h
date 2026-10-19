#pragma once
#include <windows.h>
#include <string>

namespace aurora {
inline bool EnsurePbrRuntimeDefaults(const std::wstring& config) {
    wchar_t existing[32]{};
    GetPrivateProfileStringW(L"runtime",L"default_pbr",L"__missing__",existing,32,config.c_str());
    if (wcscmp(existing,L"__missing__")!=0) return true;
    // The renderer defaults to authored profiles only. Aurora's graphics module
    // also enables ordinary models, but must retain an explicit user choice.
    return WritePrivateProfileStringW(L"runtime",L"default_pbr",L"1",config.c_str())!=FALSE;
}
}
