cmake_minimum_required(VERSION 3.24)
project(SemiSim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTOMOC_COMPILER_PREDEFINES OFF)
find_package(Qt6 REQUIRED COMPONENTS Core Gui)

set(NATIVE_HEADERS
    include/Interop/SemiSimApi.h
    include/Model/Wafer.h
    include/Model/Layer.h
    include/Model/Material.h
    include/Process/Oxidation.h
    include/Process/Photo.h
    include/Process/Etching.h
    include/Process/Deposition.h
    include/Process/Implantation.h
    include/Process/CMP.h
    include/Scene/WaferRenderer.h
    include/Animation/AnimationPlayer.h
    include/Animation/Timeline.h
    include/Resource/ModelLoader.h
    include/Resource/TextureLoader.h
    include/Utils/Config.h
)

set(NATIVE_SOURCES
    src/Interop/SemiSimApi.cpp
    src/Model/Wafer.cpp
    src/Model/Layer.cpp
    src/Model/Material.cpp
    src/Process/Oxidation.cpp
    src/Process/Photo.cpp
    src/Process/Etching.cpp
    src/Process/Deposition.cpp
    src/Process/Implantation.cpp
    src/Process/CMP.cpp
    src/Animation/AnimationPlayer.cpp
    src/Animation/Timeline.cpp
    src/Resource/ModelLoader.cpp
    src/Resource/TextureLoader.cpp
    src/Utils/Config.cpp
    src/Scene/WaferRenderer.cpp
)

add_library(SemiSimNative SHARED ${NATIVE_HEADERS} ${NATIVE_SOURCES})
target_include_directories(SemiSimNative PRIVATE include)
target_compile_definitions(SemiSimNative PRIVATE SEMISIM_WITH_QT=1)
target_link_libraries(SemiSimNative PRIVATE Qt6::Core Qt6::Gui)
# The WPF app loads "SemiSimNative.dll"; MinGW would otherwise add a "lib" prefix.
set_target_properties(SemiSimNative PROPERTIES PREFIX "")
