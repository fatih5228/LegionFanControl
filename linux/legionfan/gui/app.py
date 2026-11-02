"""legion-fan-control arayuzu - giris noktasi.

Kullanim: legion-fan-control [--tray] [--page N]
  --tray    pencereyi gostermeden sistem tepsisinde baslar (oturum acilisi icin)
  --page N  N. sayfa ile acilir (0-3; ekran goruntusu icin)
"""

import os
import sys

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QPalette
from PySide6.QtNetwork import QLocalServer, QLocalSocket
from PySide6.QtWidgets import QApplication

from .client import Client
from .widgets import C
from .window import MainWindow, load_settings

SOCKET_NAME = f"legion-fan-control-{os.getuid()}"


def _dark_palette():
    pal = QPalette()
    for role, color in ((QPalette.Window, C.BG), (QPalette.Base, C.SUBCARD), (QPalette.Text, C.TEXT),
                        (QPalette.WindowText, C.TEXT), (QPalette.Button, C.CARD), (QPalette.ButtonText, C.TEXT),
                        (QPalette.Highlight, C.RED), (QPalette.HighlightedText, C.TEXT),
                        (QPalette.ToolTipBase, C.SUBCARD), (QPalette.ToolTipText, C.TEXT),
                        (QPalette.Link, C.CYAN)):
        pal.setColor(role, QColor(color))
    return pal


def main():
    tray_only = "--tray" in sys.argv[1:]
    QApplication.setHighDpiScaleFactorRoundingPolicy(Qt.HighDpiScaleFactorRoundingPolicy.PassThrough)
    app = QApplication(sys.argv)
    app.setApplicationName("legion-fan-control")
    app.setApplicationDisplayName("Legion Y520 Fan Control")
    # Wayland'de gorev cubugu simgesi .desktop dosyasindan alinir
    app.setDesktopFileName("legion-fan-control")
    app.setQuitOnLastWindowClosed(False)
    app.setStyle("Fusion")
    app.setPalette(_dark_palette())

    # Tek kopya: uygulama zaten aciksa ona "goster" de ve cik
    sock = QLocalSocket()
    sock.connectToServer(SOCKET_NAME)
    if sock.waitForConnected(300):
        if not tray_only:
            sock.write(b"show")
            sock.waitForBytesWritten(300)
        sock.disconnectFromServer()
        return 0

    QLocalServer.removeServer(SOCKET_NAME)
    server = QLocalServer()
    server.listen(SOCKET_NAME)

    load_settings()
    client = Client()
    win = MainWindow(client)

    def on_connection():
        conn = server.nextPendingConnection()
        conn.waitForReadyRead(300)
        if bytes(conn.readAll()) == b"show":
            win.show_window()
        conn.disconnectFromServer()
    server.newConnection.connect(on_connection)

    if "--page" in sys.argv[1:-1]:
        win.select_page(int(sys.argv[sys.argv.index("--page") + 1]))
    if not tray_only:
        win.show_window()
    rc = app.exec()
    client.stop()
    return rc


if __name__ == "__main__":
    sys.exit(main())
