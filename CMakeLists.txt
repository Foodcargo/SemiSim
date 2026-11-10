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
)

if(Qt6_FOUND)
    add_executable(SemiSim ${PROJECT_HEADERS} ${PROJECT_SOURCES})
    target_include_directories(SemiSim PRIVATE include)
    target_compile_definitions(SemiSim PRIVATE SEMISIM_WITH_QT=1)
    target_link_libraries(SemiSim PRIVATE Qt6::Widgets)
else()
    add_executable(SemiSim main.cpp)
    target_compile_definitions(SemiSim PRIVATE SEMISIM_WITH_QT=0)
    message(WARNING "Qt6 was not found. Building a console fallback. Install Qt6 and set CMAKE_PREFIX_PATH to enable the GUI.")
endif()
