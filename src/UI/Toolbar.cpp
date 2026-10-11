#include "UI/Toolbar.h"

#include <QAction>

Toolbar::Toolbar(QWidget *parent)
    : QToolBar(parent)
{
    setMovable(false);
    addAction("Select");
    addAction("Pan");
    addAction("Zoom");
    addSeparator();
    addAction("Rect");
    addAction("Path");
    addAction("Via");
    addAction("Move");
    addAction("Rotate");
    addSeparator();
    addAction("DRC");
    addAction("LVS");
    auto *resetAction = addAction("Reload");
    connect(resetAction, &QAction::triggered, this, &Toolbar::resetRequested);
}
