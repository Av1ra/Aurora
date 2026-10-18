#include "PbrParser.h"
#include "Common.h"
#include <filesystem>
#include <cstdio>
int wmain(int argc,wchar_t** argv){if(argc!=2)return 2;int count=0;for(auto& file:std::filesystem::recursive_directory_iterator(argv[1]))if(file.path().extension()==L".pbr"){
 std::string text,error;pbr::PbrDocument doc;if(!pbr::ReadFileBytes(file.path().wstring(),&text)||!pbr::ParsePbrDocument(text,&doc,&error)){printf("FAIL %s\n",error.c_str());return 1;}
 for(auto& rule:doc.rules){pbr::PbrResolvedMaterial material;if(!doc.Resolve(rule.name,&material))return 3;}
 ++count;
 }printf("PASS actual runtime parser: %d exported companions\n",count);return count?0:4;}
