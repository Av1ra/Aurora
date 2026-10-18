#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <string>
#include <fstream>
int wmain(){
 wchar_t exe[32768]{};GetModuleFileNameW(nullptr,exe,32768);std::wstring root=exe;root.resize(root.find_last_of(L'\\'));
 wchar_t cwd[32768]{};GetCurrentDirectoryW(32768,cwd);
 {std::ofstream stage((root+L"\\stage.txt").c_str());stage<<"entered main";}
 bool ok=GetModuleHandleW(L"pbr.dll")&&GetModuleHandleW(L"m.dll")&&root==cwd;
 {std::ofstream stage((root+L"\\stage.txt").c_str());stage<<"before WARP";}
 ID3D11Device* device=nullptr;ID3D11DeviceContext* context=nullptr;auto hr=D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,nullptr,&context);ok=ok&&SUCCEEDED(hr);if(context)context->Release();if(device)device->Release();
 {std::ofstream stage((root+L"\\stage.txt").c_str());stage<<"after WARP";}
 Sleep(200);std::ofstream output(root+L"\\aurora-smoke-result.txt");output<<(ok?"PASS verified early modules, WARP, CWD":"FAIL fixture");return ok?0:1;
}
