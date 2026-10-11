#pragma once

#include <QToolBar>

class Toolbar : public QToolBar {
    Q_OBJECT

public:
    explicit Toolbar(QWidget *parent = nullptr);

signals:
    void resetRequested();
};
