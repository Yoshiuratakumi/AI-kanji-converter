cmake_minimum_required(VERSION 3.20)
project(AiImeCore VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ── ONNX Runtime ──────────────────────────────────────────────────────────────
# Pass -DONNXRUNTIME_ROOT=<dir> to cmake, or set env var ONNXRUNTIME_ROOT.
# Download prebuilt package from:
#   https://github.com/microsoft/onnxruntime/releases
#   e.g. onnxruntime-win-x64-1.20.1.zip
if(NOT DEFINED ONNXRUNTIME_ROOT)
    if(DEFINED ENV{ONNXRUNTIME_ROOT})
        set(ONNXRUNTIME_ROOT $ENV{ONNXRUNTIME_ROOT})
    else()
        message(FATAL_ERROR
            "ONNX Runtime not found.\n"
            "Download from https://github.com/microsoft/onnxruntime/releases\n"
            "Then run:  cmake -DONNXRUNTIME_ROOT=C:/onnxruntime-win-x64-1.20.1 .."
        )
    endif()
endif()

# MinGW uses libonnxruntime.a (generated via dlltool), MSVC uses onnxruntime.lib
if(MINGW)
    find_library(ORT_LIB
        NAMES onnxruntime
        PATHS "${ONNXRUNTIME_ROOT}/lib"
        NO_DEFAULT_PATH
        REQUIRED
    )
else()
    find_library(ORT_LIB
        NAMES onnxruntime
        PATHS "${ONNXRUNTIME_ROOT}/lib"
        NO_DEFAULT_PATH
        REQUIRED
    )
endif()

message(STATUS "ONNX Runtime: ${ONNXRUNTIME_ROOT}")
message(STATUS "ORT lib:      ${ORT_LIB}")
message(STATUS "Compiler:     ${CMAKE_CXX_COMPILER_ID}")

# ── Sources ───────────────────────────────────────────────────────────────────
add_library(AiImeCore SHARED
    src/dllmain.cpp
    src/AiImeCore.cpp
    src/BertTokenizer.cpp
    src/Reranker.cpp
    src/Dictionary.cpp
)

target_include_directories(AiImeCore PRIVATE
    include
    src
    "${ONNXRUNTIME_ROOT}/include"
)

target_compile_definitions(AiImeCore PRIVATE
    AIIMECORE_EXPORTS
    UNICODE
    _UNICODE
    # MSVC SAL annotations not available in MinGW — define them away
    $<$<CXX_COMPILER_ID:GNU>:_Frees_ptr_opt_=>
    $<$<CXX_COMPILER_ID:GNU>:_In_=>
    $<$<CXX_COMPILER_ID:GNU>:_Out_=>
    $<$<CXX_COMPILER_ID:GNU>:_Inout_=>
    $<$<CXX_COMPILER_ID:GNU>:_In_opt_=>
    $<$<CXX_COMPILER_ID:GNU>:_Out_opt_=>
)

target_compile_options(AiImeCore PRIVATE
    $<$<CXX_COMPILER_ID:MSVC>:/W4 /utf-8>
    $<$<CXX_COMPILER_ID:GNU>:-Wall -Wextra -finput-charset=UTF-8 -fexec-charset=UTF-8>
)

target_link_libraries(AiImeCore PRIVATE ${ORT_LIB})

# MinGW: link against Windows system libraries needed for ONNX Runtime
if(MINGW)
    target_link_libraries(AiImeCore PRIVATE
        -static-libgcc -static-libstdc++
        -Wl,-Bstatic,-lwinpthread,-Bdynamic)
endif()

set_target_properties(AiImeCore PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
)

# Copy onnxruntime.dll next to our DLL so the IME host finds it at runtime
add_custom_command(TARGET AiImeCore POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${ONNXRUNTIME_ROOT}/lib/onnxruntime.dll"
        "$<TARGET_FILE_DIR:AiImeCore>"
    COMMENT "Copying onnxruntime.dll to output dir"
)

# ── Tests ─────────────────────────────────────────────────────────────────────
option(BUILD_TESTS "Build unit tests" ON)

if(BUILD_TESTS)
    add_executable(test_core tests/test_core.cpp)

    target_include_directories(test_core PRIVATE include)

    target_compile_definitions(test_core PRIVATE
    UNICODE _UNICODE
    $<$<CXX_COMPILER_ID:GNU>:_Frees_ptr_opt_=>
    $<$<CXX_COMPILER_ID:GNU>:_In_=>
    $<$<CXX_COMPILER_ID:GNU>:_Out_=>
    $<$<CXX_COMPILER_ID:GNU>:_Inout_=>
    $<$<CXX_COMPILER_ID:GNU>:_In_opt_=>
    $<$<CXX_COMPILER_ID:GNU>:_Out_opt_=>
)

    target_compile_options(test_core PRIVATE
        $<$<CXX_COMPILER_ID:MSVC>:/W4 /utf-8>
        $<$<CXX_COMPILER_ID:GNU>:-Wall -finput-charset=UTF-8 -fexec-charset=UTF-8>
    )

    # Link against the DLL import library
    target_link_libraries(test_core PRIVATE AiImeCore)

    set_target_properties(test_core PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    )

    # Copy onnxruntime.dll next to test binary as well
    add_custom_command(TARGET test_core POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${ONNXRUNTIME_ROOT}/lib/onnxruntime.dll"
            "$<TARGET_FILE_DIR:test_core>"
        COMMENT "Copying onnxruntime.dll to test dir"
    )

    enable_testing()
    add_test(NAME CoreTest
        COMMAND test_core "${CMAKE_SOURCE_DIR}/models"
        WORKING_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    )
endif()
