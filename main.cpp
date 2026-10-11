#if SEMISIM_WITH_QT
#include "Core/AppManager.h"

#include <QApplication>
#else
#include <iostream>
#endif

int main(int argc, char *argv[])
{
#if SEMISIM_WITH_QT
    QApplication app(argc, argv);

    AppManager manager;
    manager.initialize();

    return app.exec();
#else
    (void)argc;
    (void)argv;

    std::cout << "SemiSim built without Qt6.\n"
              << "Install Qt6 and set CMAKE_PREFIX_PATH to the Qt lib/cmake directory to enable the GUI.\n";
    return 0;
#endif
}
