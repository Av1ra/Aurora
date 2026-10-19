#define WIN32_LEAN_AND_MEAN
#include "PbrRuntimeDefaults.h"
#include <filesystem>
#include <iostream>
int wmain(){
 wchar_t temp[MAX_PATH],unique[MAX_PATH];GetTempPathW(MAX_PATH,temp);GetTempFileNameW(temp,L"aur",0,unique);DeleteFileW(unique);
 std::filesystem::path folder=unique;std::filesystem::create_directory(folder);auto ini=(folder/L"pbr-runtime.ini").wstring();
 bool ok=aurora::EnsurePbrRuntimeDefaults(ini)&&GetPrivateProfileIntW(L"runtime",L"default_pbr",0,ini.c_str())==1;
 WritePrivateProfileStringW(L"runtime",L"default_pbr",L"0",ini.c_str());WritePrivateProfileStringW(L"features",L"vehicles",L"0",ini.c_str());
 ok=ok&&aurora::EnsurePbrRuntimeDefaults(ini)&&GetPrivateProfileIntW(L"runtime",L"default_pbr",1,ini.c_str())==0&&GetPrivateProfileIntW(L"features",L"vehicles",1,ini.c_str())==0;
 WritePrivateProfileStringW(L"runtime",L"default_pbr",nullptr,ini.c_str());
 ok=ok&&aurora::EnsurePbrRuntimeDefaults(ini)&&GetPrivateProfileIntW(L"runtime",L"default_pbr",0,ini.c_str())==1&&GetPrivateProfileIntW(L"features",L"vehicles",1,ini.c_str())==0;
 std::filesystem::remove(ini);std::filesystem::remove(folder);
 std::cout<<(ok?"PASS PBR defaults created, explicit settings and unrelated keys preserved":"FAIL PBR runtime defaults")<<std::endl;return ok?0:1;
}
