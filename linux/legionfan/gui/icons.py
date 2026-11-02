"""Tek renkli cizgi simgeleri (Segoe MDL2 glifleri yerine).

Her simge 24x24 viewBox'li SVG govdesidir; renk cizim aninda verilir.
"""

from PySide6.QtCore import QByteArray, QRectF, Qt
from PySide6.QtGui import QIcon, QPainter, QPixmap
from PySide6.QtSvg import QSvgRenderer

_PATHS = {
    "bolt": '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
    "monitor": '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>',
    "gamepad": '<rect x="2" y="7" width="20" height="11" rx="5"/><path d="M7 10.5v4M5 12.5h4"/>'
               '<circle cx="15.5" cy="11.5" r=".6" fill="currentColor"/><circle cx="17.5" cy="13.5" r=".6" fill="currentColor"/>',
    "info": '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M12 11v6M12 7.5v.5"/>',
    "thermo": '<path d="M14 14.8V5a2 2 0 0 0-4 0v9.8a4 4 0 1 0 4 0z"/><path d="M12 9v7"/>',
    "wrench": '<path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2.4-.6-.6-2.4z"/>',
    "memory": '<path d="M5 3h11l4 4v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z"/>'
              '<path d="M8 3v5h7V3M8 21v-6h8v6"/>',
    "laptop": '<rect x="4" y="5" width="16" height="11" rx="1.5"/><path d="M2 19h20"/>',
    "gear": '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M2 12h3M19 12h3'
            'M4.9 19.1 7 17M17 7l2.1-2.1"/>',
    "square": '<rect x="6" y="6" width="12" height="12" rx="1.5"/>',
    "mouse": '<rect x="7" y="3" width="10" height="18" rx="5"/><path d="M12 7v3"/>',
    "rocket": '<path d="M5 15c-1.5 1.3-2 5-2 5s3.7-.5 5-2c.7-.8.7-2.1-.1-2.9a2.2 2.2 0 0 0-2.9-.1z"/>'
              '<path d="m12 15-3-3a22 22 0 0 1 2-4A12.9 12.9 0 0 1 22 2c0 2.7-.8 7.5-6 11a22.4 22.4 0 0 1-4 2z"/>'
              '<path d="M9 12H4s.6-3 2-4c1.6-1.1 5 0 5 0M12 15v5s3-.6 4-2c1.1-1.6 0-5 0-5"/>',
    "github": '<path d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.9a3.4 3.4 0 0 0-.9-2.6c3.1-.4 6.4-1.5 6.4-7a5.4 5.4 0 0 0-1.5-3.7'
              ' 5 5 0 0 0-.1-3.8s-1.2-.3-3.9 1.5a13.4 13.4 0 0 0-7 0C6.3 1.7 5.1 2 5.1 2a5 5 0 0 0-.1 3.8A5.4 5.4 0 0 0 3.5 9.5'
              'c0 5.4 3.3 6.6 6.4 7a3.4 3.4 0 0 0-.9 2.6V22"/>',
    "minimize": '<path d="M6 12h12"/>',
    "close": '<path d="M6 6l12 12M18 6 6 18"/>',
}


def _svg(name, color, width):
    body = _PATHS[name].replace("currentColor", color)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="{color}" '
            f'stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round">{body}</svg>')


def pixmap(name, color, size, width=1.8, dpr=2.0):
    """Simgeyi istenen renkte, HiDPI ekranlarda keskin kalacak sekilde cizer."""
    px = QPixmap(int(size * dpr), int(size * dpr))
    px.fill(Qt.transparent)
    renderer = QSvgRenderer(QByteArray(_svg(name, color, width).encode()))
    p = QPainter(px)
    p.setRenderHint(QPainter.Antialiasing)
    renderer.render(p, QRectF(0, 0, px.width(), px.height()))
    p.end()
    px.setDevicePixelRatio(dpr)
    return px


def icon(name, color, size=16):
    return QIcon(pixmap(name, color, size))
