#pragma once
#include <cstddef>
#include <cstring>
#include <limits>
namespace aurora::fov {
inline constexpr float table[]={.7777777778f,.78f,.7853981633974483f,.8f};
inline constexpr size_t missing=std::numeric_limits<size_t>::max();
inline size_t FindTable(const unsigned char* bytes,size_t size){
 size_t result=missing;
 for(size_t i=0;i+sizeof(table)<=size;i+=4)if(!std::memcmp(bytes+i,table,sizeof(table))){if(result!=missing)return missing;result=i+8;}
 return result;
}
}
