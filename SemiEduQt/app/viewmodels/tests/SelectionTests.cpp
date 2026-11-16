#include <QCoreApplication>
#include <QDebug>
#include <QJsonArray>
#include <cstdlib>
#include <cstdio>
#include "AppViewModel.h"
#include "ProcessEditorViewModel.h"
#include "ResultViewModels.h"
#include "engine/EngineClient.h"

static void check(bool value, const char *message)
{
    if (!value) { std::fprintf(stderr, "FAIL: %s\n", message); std::exit(1); }
}

int main(int argc, char **argv)
{
    QCoreApplication app(argc, argv);
    check(argc == 2, "Provide process-only script path");
    EngineClient engine;
    check(engine.load(), "Load native engine");
    check(engine.supportsUndoTo(), "Native undo export exists");
    ProcessEditorViewModel editor(&engine);
    ResultViewModel result;
    SceneViewModel scene(&engine);
    PlotViewModel plot;
    AppViewModel vm(&engine, &editor, &result, &scene, &plot);
    vm.runScriptFile(QString::fromLocal8Bit(argv[1]));
    auto activeCount = [&] { return engine.state().value("activeRuns").toArray().size(); };
    check(activeCount() == 9, "Replay nine process steps");
    int warnings = 0, later = 0;
    QObject::connect(&vm, &AppViewModel::undoConfirmationRequested,
                     [&](const QString &, int count) { ++warnings; later = count; });
    QObject::connect(&vm, &AppViewModel::undoError, [&](const QString &) { check(false, "Unexpected undo error"); });
    vm.selectLayer("SUB");
    check(vm.selectedLayerText().contains("Silicon"), "Substrate material description");
    auto layers = engine.state().value("layers").toArray();
    check(!layers.isEmpty(), "Film layers exist");
    vm.selectLayer(layers.first().toObject().value("layerId").toString());
    check(vm.selectedLayerText().contains("Material:"), "Film material description");
    vm.selectProcess("oxidation");
    vm.requestUndoSelected();
    check(warnings == 1 && later == 4 && activeCount() == 9, "Earlier run requires one warning without mutation");
    vm.discardUndoRequest();
    vm.confirmUndoSelected();
    check(activeCount() == 9, "Cancel keeps state");
    vm.requestUndoSelected();
    vm.confirmUndoSelected();
    check(activeCount() == 4, "Confirm removes selected and later runs");
    check(vm.selectedLayerId().isEmpty(), "Removed layer selection clears");
    vm.selectProcess("mask");
    int beforeWarnings = warnings;
    vm.requestUndoSelected();
    check(warnings == beforeWarnings && activeCount() == 3, "Latest repeated process undoes immediately");
    check(engine.state().value("masks").toArray().size() == 2, "Mask library restored");
    vm.selectProcess("contact");
    check(!vm.canUndoSelected(), "Undone process has no active target");
    vm.requestUndoSelected();
    check(activeCount() == 3, "Inactive selection does not undo");
    std::puts("PASS: native ABI, warning, cancel, suffix undo, latest run, masks, layer descriptions and stale selection");
    return 0;
}
