cmake_minimum_required(VERSION 3.20)
project(SSRHook LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(SSRHook SHARED SSRHook.cpp)
target_link_libraries(SSRHook PRIVATE d3d11)

# Build (from this dir):
#   cmake -B build -A x64
#   cmake --build build --config Release
# load output via BPR modder with SSRHook.ini beside it
