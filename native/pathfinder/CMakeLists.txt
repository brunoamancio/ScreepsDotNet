cmake_minimum_required(VERSION 3.16)
project(ScreepsPathfinder LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

add_library(screeps_pathfinder SHARED
    pf.cc
    pathfinder_exports.cpp)

target_include_directories(screeps_pathfinder PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

# TODO: add V8/NAN dependencies and replace the stubs once the C API is finalized.
