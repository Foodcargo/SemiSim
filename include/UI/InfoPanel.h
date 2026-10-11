#pragma once

#include <QWidget>

class QLabel;
class Wafer;

class InfoPanel : public QWidget {
    Q_OBJECT

public:
    explicit InfoPanel(QWidget *parent = nullptr);

    void setWafer(Wafer *wafer);

private slots:
    void refresh();

private:
    Wafer *m_wafer = nullptr;
    QLabel *m_label = nullptr;
};
