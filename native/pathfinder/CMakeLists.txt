cmake_minimum_required(VERSION 3.16)
project(ScreepsPathfinder LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(RUNTIME_IDENTIFIER "" CACHE STRING "Target runtime identifier (e.g., linux-x64, win-x64, osx-arm64)")

if(NOT RUNTIME_IDENTIFIER)
    if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
        if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
            set(RUNTIME_IDENTIFIER "linux-arm64")
        else()
            set(RUNTIME_IDENTIFIER "linux-x64")
        endif()
    elseif(CMAKE_SYSTEM_NAME STREQUAL "Windows")
        if(CMAKE_SIZEOF_VOID_P EQUAL 4)
            set(RUNTIME_IDENTIFIER "win-x86")
        else()
            set(RUNTIME_IDENTIFIER "win-x64")
        endif()
    elseif(CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        if(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64")
            set(RUNTIME_IDENTIFIER "osx-arm64")
        else()
            set(RUNTIME_IDENTIFIER "osx-x64")
        endif()
    else()
        message(FATAL_ERROR "Unsupported platform ${CMAKE_SYSTEM_NAME}. Please set RUNTIME_IDENTIFIER manually.")
    endif()
endif()

message(STATUS "Building Screeps pathfinder for RID: ${RUNTIME_IDENTIFIER}")

add_library(screeps_pathfinder SHARED
    pf.cc
    pathfinder_exports.cpp)

target_include_directories(screeps_pathfinder PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
target_compile_definitions(screeps_pathfinder PRIVATE SCREEPS_PATHFINDER_NO_V8=1)
set_target_properties(screeps_pathfinder PROPERTIES OUTPUT_NAME screepspathfinder)

set(DRIVER_RUNTIME_DIR ${CMAKE_CURRENT_SOURCE_DIR}/../../ScreepsDotNet.Driver/runtimes/${RUNTIME_IDENTIFIER}/native)
add_custom_command(TARGET screeps_pathfinder POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${DRIVER_RUNTIME_DIR}
    COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:screeps_pathfinder> ${DRIVER_RUNTIME_DIR}/$<TARGET_FILE_NAME:screeps_pathfinder>
    COMMENT "Copying native library to ${DRIVER_RUNTIME_DIR}"
)
