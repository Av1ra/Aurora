#include "Resolver.h"
#include <vector>
#include <iostream>
int main(){std::vector<unsigned char> bytes(128);std::memcpy(bytes.data()+16,aurora::fov::table,16);if(aurora::fov::FindTable(bytes.data(),bytes.size())!=24)return 1;std::memcpy(bytes.data()+64,aurora::fov::table,16);if(aurora::fov::FindTable(bytes.data(),bytes.size())!=aurora::fov::missing)return 2;if(aurora::fov::FindTable(bytes.data(),4)!=aurora::fov::missing)return 3;std::cout<<"FOV_RESOLVER_PASS\n";}
