cmake_minimum_required(VERSION 3.24)
project(SemiEduQt VERSION 1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(Qt6 6.5 REQUIRED COMPONENTS Core Gui Qml Quick QuickControls2 OpenGL Concurrent)
qt_standard_project_setup(REQUIRES 6.5)

# Folder that contains the NativeAOT engine (dotnet publish ... -o out/engine).
set(SEMIEDU_ENGINE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/out/engine" CACHE PATH "Directory containing SemiEdu.Engine.dll")

qt_add_executable(SemiEduApp WIN32
    app/main.cpp
    app/engine/EngineClient.h
    app/engine/EngineClient.cpp
    engine/semiedu_engine.h
)

set_source_files_properties(app/qml/Theme.qml PROPERTIES QT_QML_SINGLETON_TYPE TRUE)

qt_add_qml_module(SemiEduApp
    URI SemiEdu
    VERSION 1.0
    QML_FILES
        app/qml/Main.qml
        app/qml/ProcessList.qml
        app/qml/ParamEditor.qml
        app/qml/ParamField.qml
        app/qml/ResultPanel.qml
        app/qml/ObjectTable.qml
        app/qml/HistoryPanel.qml
        app/qml/ViewHeader.qml
        app/qml/LayerManager.qml
        app/qml/DockTitle.qml
        app/qml/SectionHeader.qml
        app/qml/ToolBtn.qml
        app/qml/Theme.qml
    SOURCES
        app/viewmodels/ListModels.h
        app/viewmodels/ListModels.cpp
        app/viewmodels/ProcessEditorViewModel.h
        app/viewmodels/ProcessEditorViewModel.cpp
        app/viewmodels/ResultViewModels.h
        app/viewmodels/ResultViewModels.cpp
        app/viewmodels/AppViewModel.h
        app/viewmodels/AppViewModel.cpp
        app/render/WaferView.h
        app/render/WaferView.cpp
        app/render/PlotItem.h
        app/render/PlotItem.cpp
)

# qmltyperegistrar includes QML_ELEMENT headers by bare file name.
target_include_directories(SemiEduApp PRIVATE app app/viewmodels app/render engine)
target_link_libraries(SemiEduApp PRIVATE
    Qt6::Core Qt6::Gui Qt6::Qml Qt6::Quick Qt6::QuickControls2 Qt6::OpenGL Qt6::Concurrent)

# Learning-path scripts next to the exe (File ▸ 학습 경로 열기 starts in this folder).
add_custom_command(TARGET SemiEduApp POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_CURRENT_SOURCE_DIR}/scripts" "$<TARGET_FILE_DIR:SemiEduApp>/scripts")

if(EXISTS "${SEMIEDU_ENGINE_DIR}/SemiEdu.Engine.dll")
    add_custom_command(TARGET SemiEduApp POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "${SEMIEDU_ENGINE_DIR}/SemiEdu.Engine.dll" "$<TARGET_FILE_DIR:SemiEduApp>"
        COMMENT "Copying SemiEdu.Engine.dll next to SemiEduApp.exe")
else()
    message(WARNING "SemiEdu.Engine.dll not found in SEMIEDU_ENGINE_DIR='${SEMIEDU_ENGINE_DIR}'. Publish the engine first.")
endif()
