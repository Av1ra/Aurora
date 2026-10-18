#include "Sha256.h"
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <cstdio>
#include <vector>
#pragma comment(lib, "bcrypt.lib")

namespace pbr {
std::string Sha256File(const std::wstring& path) {
    BCRYPT_ALG_HANDLE alg = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    DWORD objectSize = 0, hashSize = 0, got = 0;
    if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0) return {};
    if (BCryptGetProperty(alg, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&objectSize), sizeof(objectSize), &got, 0) < 0 ||
        BCryptGetProperty(alg, BCRYPT_HASH_LENGTH, reinterpret_cast<PUCHAR>(&hashSize), sizeof(hashSize), &got, 0) < 0) {
        BCryptCloseAlgorithmProvider(alg, 0); return {};
    }

    std::vector<UCHAR> object(objectSize), digest(hashSize);
    if (BCryptCreateHash(alg, &hash, object.data(), objectSize, nullptr, 0, 0) < 0) {
        BCryptCloseAlgorithmProvider(alg, 0); return {};
    }

    FILE* f = nullptr; _wfopen_s(&f, path.c_str(), L"rb");
    if (!f) { BCryptDestroyHash(hash); BCryptCloseAlgorithmProvider(alg, 0); return {}; }

    std::vector<UCHAR> buffer(1 << 20);
    bool ok = true;
    while (true) {
        const std::size_t n = std::fread(buffer.data(), 1, buffer.size(), f);
        if (n && BCryptHashData(hash, buffer.data(), static_cast<ULONG>(n), 0) < 0) { ok = false; break; }
        if (n < buffer.size()) { if (std::ferror(f)) ok = false; break; }
    }
    std::fclose(f);

    std::string result;
    if (ok && BCryptFinishHash(hash, digest.data(), hashSize, 0) >= 0) {
        static const char hex[] = "0123456789abcdef";
        result.reserve(digest.size() * 2);
        for (UCHAR v : digest) { result.push_back(hex[v >> 4]); result.push_back(hex[v & 15]); }
    }
    BCryptDestroyHash(hash); BCryptCloseAlgorithmProvider(alg, 0);
    return result;
}
}  // namespace pbr
