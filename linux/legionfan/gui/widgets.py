"""Ortak arayuz parcalari: renk paleti, kart, anahtar, fan cubugu, simge kutusu."""

from PySide6.QtCore import (Property, QEasingCurve, QPropertyAnimation, QRectF,
                            QSize, Qt, Signal)
from PySide6.QtGui import QColor, QFont, QLinearGradient, QPainter, QPen
from PySide6.QtWidgets import QFrame, QLabel, QSizePolicy, QWidget

from . import icons, lang


# ---------------- Renk paleti (Program.cs ile ayni) ----------------
class C:
    BG = "#0C0E12"
    SIDEBAR = "#12151B"
    TITLEBAR = "#090A0D"
    CARD = "#161A22"
    SUBCARD = "#101318"
    EDGE = QColor(255, 255, 255, 0x22)
    SEL = "#1F2430"
    RED = "#E2231A"
    RED_GLOW = "#FF382E"
    RED_DARK = "#8B120D"
    SWITCH_OFF = "#242934"
    TRACK = "#101216"
    GREEN = "#10B981"
    ORANGE = "#F59E0B"
    CYAN = "#06B6D4"
    TEXT = "#FFFFFF"
    TEXT2 = "#A2A9B8"
    MUTED = "#646C7D"


def label(text="", color=C.TEXT, size=12, weight=QFont.Normal, wrap=False):
    lb = QLabel(text)
    f = lb.font()
    f.setPixelSize(size)
    f.setWeight(weight)
    lb.setFont(f)
    lb.setStyleSheet(f"color: {color}; background: transparent;")
    lb.setWordWrap(wrap)
    return lb


def set_color(lb, color):
    lb.setStyleSheet(f"color: {color}; background: transparent;")


class Card(QFrame):
    """Yuvarlak koseli kart; cerceve rengi durumla degisir (sicaklik, Extreme Cooling)."""

    def __init__(self, parent=None, radius=12, bg=C.CARD):
        super().__init__(parent)
        self._radius = radius
        self._bg = QColor(bg)
        self._edge = QColor(C.EDGE)

    def set_edge(self, color):
        self._edge = QColor(color) if color is not None else QColor(C.EDGE)
        self.update()

    def paintEvent(self, _):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        p.setPen(QPen(self._edge, 1))
        p.setBrush(self._bg)
        p.drawRoundedRect(QRectF(self.rect()).adjusted(0.5, 0.5, -0.5, -0.5), self._radius, self._radius)


class IconBox(QWidget):
    """Kart basligindaki simge kutusu; hero=True kirmizi degrade (Extreme Cooling)."""

    def __init__(self, name, color, size=40, icon_size=None, hero=False, parent=None):
        super().__init__(parent)
        self.setFixedSize(size, size)
        self._name, self._color, self._hero = name, color, hero
        self._icon_size = icon_size or int(size * 0.5)
        self._px = icons.pixmap(name, color, self._icon_size, width=2.0)

    def paintEvent(self, _):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(0.5, 0.5, -0.5, -0.5)
        radius = r.width() * 0.24
        if self._hero:
            g = QLinearGradient(r.topLeft(), r.bottomRight())
            g.setColorAt(0, QColor(C.RED_DARK))
            g.setColorAt(1, QColor("#3A0805"))
            p.setBrush(g)
            p.setPen(QPen(QColor(226, 35, 26, 0x90), 1))
        else:
            p.setBrush(QColor(255, 255, 255, 0x14))
            p.setPen(QPen(QColor(255, 255, 255, 0x30), 1))
        p.drawRoundedRect(r, radius, radius)
        x = (self.width() - self._icon_size) / 2
        y = (self.height() - self._icon_size) / 2
        p.drawPixmap(int(x), int(y), self._px)


class ModernSwitch(QWidget):
    """AÇIK/KAPALI etiketli, kayan topuzlu anahtar (WPF ModernSwitch karsiligi)."""

    toggled = Signal(bool)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedSize(76, 30)
        self.setCursor(Qt.PointingHandCursor)
        self._checked = False
        self._pos = 0.0
        self._anim = QPropertyAnimation(self, b"knob", self)
        self._anim.setDuration(160)
        self._anim.setEasingCurve(QEasingCurve.OutCubic)

    def _get_knob(self):
        return self._pos

    def _set_knob(self, v):
        self._pos = v
        self.update()

    knob = Property(float, _get_knob, _set_knob)

    def isChecked(self):
        return self._checked

    def set_checked_quietly(self, val, animate=True):
        # Sinyal uretmeden gorunumu gunceller (servisten gelen durum icin)
        if val == self._checked:
            return
        self._checked = val
        target = 1.0 if val else 0.0
        if animate and self.isVisible():
            self._anim.stop()
            self._anim.setStartValue(self._pos)
            self._anim.setEndValue(target)
            self._anim.start()
        else:
            self._set_knob(target)

    def mouseReleaseEvent(self, e):
        if e.button() == Qt.LeftButton and self.isEnabled() and self.rect().contains(e.position().toPoint()):
            self.toggled.emit(not self._checked)

    def paintEvent(self, _):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(0.5, 0.5, -0.5, -0.5)
        h = r.height()
        on = self._checked
        if not self.isEnabled():
            p.setOpacity(0.45)
        p.setPen(QPen(QColor(C.EDGE) if not on else QColor(C.RED_GLOW), 1))
        p.setBrush(QColor(C.RED) if on else QColor(C.SWITCH_OFF))
        p.drawRoundedRect(r, h / 2, h / 2)

        f = QFont(self.font())
        f.setPixelSize(10)
        f.setWeight(QFont.Bold)
        p.setFont(f)
        p.setPen(QColor(C.TEXT) if on else QColor(C.TEXT2))
        knob_d = h - 8
        if on:
            p.drawText(QRectF(r.left() + 10, r.top(), r.width() - knob_d - 14, h),
                       Qt.AlignVCenter | Qt.AlignLeft, lang.T("switch.on"))
        else:
            p.drawText(QRectF(r.left() + knob_d + 8, r.top(), r.width() - knob_d - 14, h),
                       Qt.AlignVCenter | Qt.AlignRight, lang.T("switch.off"))

        x = r.left() + 4 + self._pos * (r.width() - knob_d - 8)
        p.setPen(Qt.NoPen)
        p.setBrush(QColor(C.TEXT))
        p.drawEllipse(QRectF(x, r.top() + 4, knob_d, knob_d))


class FanBar(QWidget):
    """Fan devri cubugu; doluluk = rpm / maks. devir."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedHeight(8)
        self.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Fixed)
        self._ratio = 0.0

    def set_ratio(self, ratio):
        self._ratio = max(0.0, min(1.0, ratio))
        self.update()

    def sizeHint(self):
        return QSize(200, 8)

    def paintEvent(self, _):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect())
        p.setPen(QPen(QColor(C.EDGE), 1))
        p.setBrush(QColor(C.TRACK))
        p.drawRoundedRect(r.adjusted(0.5, 0.5, -0.5, -0.5), 4, 4)
        if self._ratio > 0:
            fill = QRectF(r.left(), r.top(), max(r.height(), r.width() * self._ratio), r.height())
            g = QLinearGradient(fill.topLeft(), fill.topRight())
            g.setColorAt(0, QColor(C.RED_DARK))
            g.setColorAt(1, QColor(C.RED_GLOW))
            p.setPen(Qt.NoPen)
            p.setBrush(g)
            p.drawRoundedRect(fill, 4, 4)


SLIDER_QSS = f"""
QSlider::groove:horizontal {{ height: 4px; background: {C.SWITCH_OFF}; border-radius: 2px; }}
QSlider::sub-page:horizontal {{ background: {C.SWITCH_OFF}; border-radius: 2px; }}
QSlider::handle:horizontal {{
    background: {C.RED}; border: 3px solid #FFFFFF;
    width: 12px; height: 12px; margin: -7px 0; border-radius: 9px;
}}
QSlider::handle:horizontal:hover {{ background: {C.RED_GLOW}; }}
"""

SCROLL_QSS = f"""
QScrollArea {{ background: transparent; border: none; }}
QScrollArea > QWidget > QWidget {{ background: transparent; }}
QScrollBar:vertical {{ background: transparent; width: 8px; margin: 0; }}
QScrollBar::handle:vertical {{ background: {C.SWITCH_OFF}; border-radius: 4px; min-height: 30px; }}
QScrollBar::handle:vertical:hover {{ background: {C.MUTED}; }}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {{ background: transparent; }}
"""

MENU_QSS = f"""
QMenu {{ background: {C.SUBCARD}; color: {C.TEXT}; border: 1px solid rgba(255,255,255,34);
         border-radius: 8px; padding: 4px; }}
QMenu::item {{ padding: 6px 18px 6px 12px; border-radius: 5px; }}
QMenu::item:selected {{ background: {C.SEL}; }}
QMenu::item:disabled {{ color: {C.TEXT2}; }}
QMenu::separator {{ height: 1px; background: rgba(255,255,255,34); margin: 4px 6px; }}
"""
