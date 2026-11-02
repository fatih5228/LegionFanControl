"""Ana pencere (Program.cs MainWindow karsiligi)."""

import configparser
import json
import os
import re
import threading
import time
import urllib.request

from PySide6.QtCore import QRectF, QSize, Qt, QTimer, Signal
from PySide6.QtGui import QAction, QColor, QFont, QIcon, QPainter, QPen, QPixmap
from PySide6.QtWidgets import (QFrame, QGridLayout, QHBoxLayout, QLabel, QMenu,
                               QScrollArea, QSizeGrip, QSlider, QStackedWidget,
                               QSystemTrayIcon, QToolButton, QVBoxLayout, QWidget)

from .. import REPO_URL, THRESHOLD_DEFAULT, THRESHOLD_MAX, THRESHOLD_MIN, VERSION
from . import icons, lang
from .lang import F, T
from .widgets import (MENU_QSS, SCROLL_QSS, SLIDER_QSS, C, Card, FanBar, IconBox,
                      ModernSwitch, label, set_color)

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG_DIR = os.path.join(os.environ.get("XDG_CONFIG_HOME", os.path.expanduser("~/.config")), "legion-fan-control")
SETTINGS_PATH = os.path.join(CONFIG_DIR, "settings.ini")
AUTOSTART_PATH = os.path.join(os.environ.get("XDG_CONFIG_HOME", os.path.expanduser("~/.config")),
                              "autostart", "legion-fan-control.desktop")
RELEASES_API = "https://api.github.com/repos/fatih5228/LegionFanControl/releases/latest"
POLL_MS = 2000


def load_settings():
    cp = configparser.ConfigParser()
    cp.read(SETTINGS_PATH)
    val = cp.get("ui", "Language", fallback="")
    if val in lang.AVAILABLE:
        lang.current = val


def save_settings():
    try:
        os.makedirs(CONFIG_DIR, exist_ok=True)
        with open(SETTINGS_PATH, "w") as f:
            f.write(f"[ui]\nLanguage={lang.current}\n")
    except OSError:
        pass


def fmt_temp(t):
    return f"{round(t)} °C" if t is not None and t > 0 else "—"


def _is_newer(tag):
    def parts(s):
        return tuple(int(x) for x in re.findall(r"\d+", s)[:3])
    try:
        return parts(tag) > parts(VERSION)
    except ValueError:
        return False


class MainWindow(QWidget):
    _update_done = Signal(str, str, bool, bool)  # etiket, sayfa, basarisiz, manuel

    def __init__(self, client):
        super().__init__()
        self._client = client
        self._real_exit = False
        self._polling = False
        self._state = {}
        self._hwinfo = None
        self._locks = None
        self._max_rpm = 4000
        self._page = 0
        self._update_tag = None
        self._update_url = None
        self._banner_dismissed = False
        self._slider_busy = False

        self.setWindowTitle(T("app.title"))
        self.setWindowFlags(Qt.Window | Qt.FramelessWindowHint)
        self.setWindowIcon(QIcon(os.path.join(HERE, "icon-on.png")))
        self.resize(840, 670)
        self.setMinimumSize(780, 620)
        self.setStyleSheet(f"MainWindow {{ background: {C.BG}; }}" + MENU_QSS)
        self.setAttribute(Qt.WA_StyledBackground, True)

        self._icon_on = QIcon(os.path.join(HERE, "icon-on.png"))
        self._icon_off = QIcon(os.path.join(HERE, "icon-off.png"))

        self._root = QVBoxLayout(self)
        self._root.setContentsMargins(0, 0, 0, 0)
        self._root.setSpacing(0)
        self._body = None
        self._build_ui()
        self._build_tray()

        self._threshold_timer = QTimer(self)
        self._threshold_timer.setSingleShot(True)
        self._threshold_timer.setInterval(400)
        self._threshold_timer.timeout.connect(self._send_threshold)
        self._update_done.connect(self._on_update_done)

        self._timer = QTimer(self)
        self._timer.setInterval(POLL_MS)
        self._timer.timeout.connect(self._poll)
        self._timer.start()
        self._poll()
        self._load_hardware_info()
        self._refresh_locks()
        self._check_updates(manual=False)

    # ---------------- Pencere ----------------
    def closeEvent(self, e):
        # Kapat dugmesi pencereyi gizler; uygulama tepside calismaya devam eder
        if not self._real_exit and self._tray.isVisible():
            e.ignore()
            self.hide()
            return
        self._timer.stop()
        self._tray.hide()
        e.accept()

    def show_window(self):
        self.show()
        self.raise_()
        self.activateWindow()

    def real_exit(self):
        self._real_exit = True
        self.close()

    # ---------------- Arayuz ----------------
    def _build_ui(self):
        # Dil degisiminde tum govde yeniden kurulur
        if self._body is not None:
            self._root.removeWidget(self._body)
            self._body.deleteLater()
        self._body = QWidget()
        lay = QVBoxLayout(self._body)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(0)
        lay.addWidget(self._build_titlebar())

        main = QHBoxLayout()
        main.setContentsMargins(0, 0, 0, 0)
        main.setSpacing(0)
        main.addWidget(self._build_sidebar())

        right = QVBoxLayout()
        right.setContentsMargins(0, 0, 0, 0)
        right.setSpacing(0)
        right.addWidget(self._build_update_banner())
        self._stack = QStackedWidget()
        self._stack.setStyleSheet("background: transparent;")
        for page in (self._build_fan_page(), self._build_hardware_page(),
                     self._build_system_page(), self._build_about_page()):
            self._stack.addWidget(self._scroll(page))
        host = QVBoxLayout()
        host.setContentsMargins(20, 12, 12, 6)
        host.addWidget(self._stack)
        right.addLayout(host, 1)
        right.addWidget(self._build_statusbar())
        main.addLayout(right, 1)
        lay.addLayout(main, 1)
        self._root.addWidget(self._body)

        self.setWindowTitle(T("app.title"))
        self.select_page(self._page)
        self._apply_state()
        self._render_hardware()
        self._apply_locks()
        self._show_update_banner()

    def _scroll(self, page):
        sa = QScrollArea()
        sa.setWidgetResizable(True)
        sa.setFrameShape(QFrame.NoFrame)
        sa.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        sa.setStyleSheet(SCROLL_QSS)
        wrap = QWidget()
        wl = QVBoxLayout(wrap)
        wl.setContentsMargins(0, 0, 8, 0)
        wl.addWidget(page)
        wl.addStretch(1)
        sa.setWidget(wrap)
        return sa

    # ---- Baslik cubugu ----
    def _build_titlebar(self):
        bar = _TitleBar(self)
        bar.setFixedHeight(40)
        bar.setStyleSheet(f"background: {C.TITLEBAR};")
        h = QHBoxLayout(bar)
        h.setContentsMargins(14, 0, 0, 0)
        h.setSpacing(0)
        logo = QLabel()
        logo.setPixmap(QPixmap(os.path.join(HERE, "icon-on.png")))
        logo.setFixedSize(20, 20)
        logo.setScaledContents(True)
        h.addWidget(logo)
        h.addSpacing(10)
        h.addWidget(label("LEGION Y520", C.TEXT, 12, QFont.Bold))
        h.addSpacing(4)
        h.addWidget(label(T("titlebar.sub"), C.RED, 12, QFont.DemiBold))
        h.addStretch(1)

        btn = QToolButton()
        btn.setText(f"{lang.current.upper()}  ▾")
        btn.setCursor(Qt.PointingHandCursor)
        btn.setPopupMode(QToolButton.InstantPopup)
        btn.setStyleSheet(
            f"QToolButton {{ color: {C.TEXT}; background: transparent; border: 1px solid rgba(255,255,255,34);"
            f" border-radius: 7px; padding: 0 10px; font-size: 11px; font-weight: 600; }}"
            f"QToolButton:hover {{ background: {C.SEL}; }} QToolButton::menu-indicator {{ image: none; width: 0; }}")
        menu = QMenu(btn)
        for code in lang.AVAILABLE:
            act = menu.addAction(("✓  " if code == lang.current else "     ") +
                                 ("Türkçe" if code == "tr" else "English"))
            act.triggered.connect(lambda _=False, c=code: self._change_language(c))
        btn.setMenu(menu)
        btn.setFixedHeight(26)
        h.addWidget(btn, 0, Qt.AlignVCenter)
        h.addSpacing(8)

        for name, slot, is_close in (("minimize", self.showMinimized, False), ("close", self.close, True)):
            b = QToolButton()
            b.setIcon(icons.icon(name, C.TEXT2, 14))
            b.setIconSize(QSize(14, 14))
            b.setFixedSize(46, 40)
            hover = C.RED if is_close else C.SEL
            b.setStyleSheet(f"QToolButton {{ background: transparent; border: none; }}"
                            f"QToolButton:hover {{ background: {hover}; }}")
            b.clicked.connect(slot)
            h.addWidget(b)
        return bar

    # ---- Sol menu ----
    def _build_sidebar(self):
        side = QFrame()
        side.setFixedWidth(210)
        side.setObjectName("sidebar")
        side.setStyleSheet(f"#sidebar {{ background: {C.SIDEBAR}; border-right: 1px solid rgba(255,255,255,34); }}")
        v = QVBoxLayout(side)
        v.setContentsMargins(14, 16, 14, 12)
        v.setSpacing(4)

        brand = Card(bg=C.SUBCARD, radius=10)
        bl = QHBoxLayout(brand)
        bl.setContentsMargins(14, 12, 14, 12)
        logo = QLabel()
        logo.setPixmap(QPixmap(os.path.join(HERE, "icon-on.png")).scaled(
            30, 30, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        bl.addWidget(logo)
        bt = QVBoxLayout()
        bt.setSpacing(0)
        bt.addWidget(label("LEGION", C.RED, 18, QFont.Black))
        bt.addWidget(label("GAMING ZONE", C.MUTED, 9, QFont.Bold))
        bl.addLayout(bt, 1)
        v.addWidget(brand)
        v.addSpacing(18)

        self._menu = []
        items = (("bolt", "menu.fan"), ("monitor", "menu.hw"), ("gamepad", "menu.sys"), ("info", "menu.about"))
        for i, (ic, key) in enumerate(items):
            item = _MenuItem(ic, T(key))
            item.clicked.connect(lambda i=i: self.select_page(i))
            self._menu.append(item)
            v.addWidget(item)
        v.addStretch(1)
        v.addWidget(label("Y520-15IKBN", C.TEXT2, 10, QFont.Bold))
        v.addWidget(label(f"v{VERSION} • Linux (Qt)", C.MUTED, 9))
        return side

    def select_page(self, idx):
        self._page = idx
        self._stack.setCurrentIndex(idx)
        for i, item in enumerate(self._menu):
            item.set_active(i == idx)
        if idx == 2:
            self._refresh_locks()

    # ---- Guncelleme bandi ----
    def _build_update_banner(self):
        self._banner = QFrame()
        self._banner.setObjectName("banner")
        self._banner.setStyleSheet("#banner { background: rgba(226,35,26,38); border-bottom: 1px solid rgba(255,255,255,34); }")
        h = QHBoxLayout(self._banner)
        h.setContentsMargins(16, 6, 10, 6)
        self._banner_text = label("", C.TEXT, 11)
        self._banner_text.setTextFormat(Qt.RichText)
        self._banner_text.setOpenExternalLinks(True)
        h.addWidget(self._banner_text, 1)
        x = QToolButton()
        x.setIcon(icons.icon("close", C.MUTED, 10))
        x.setFixedSize(26, 22)
        x.setStyleSheet(f"QToolButton {{ border: none; background: transparent; border-radius: 4px; }}"
                        f"QToolButton:hover {{ background: {C.SEL}; }}")
        x.clicked.connect(self._dismiss_banner)
        h.addWidget(x)
        self._banner.hide()
        return self._banner

    # ---- Durum cubugu ----
    def _build_statusbar(self):
        bar = QFrame()
        bar.setObjectName("status")
        bar.setStyleSheet(f"#status {{ background: {C.TITLEBAR}; border-top: 1px solid rgba(255,255,255,34); }}")
        h = QHBoxLayout(bar)
        h.setContentsMargins(16, 7, 2, 2)
        self._status_dot = QLabel()
        self._status_dot.setFixedSize(8, 8)
        h.addWidget(self._status_dot)
        h.addSpacing(8)
        self._status_text = label(T("status.ready"), C.TEXT2, 11)
        h.addWidget(self._status_text, 1)
        grip = QSizeGrip(bar)
        h.addWidget(grip, 0, Qt.AlignBottom | Qt.AlignRight)
        self._set_status(T("status.ready"), False)
        return bar

    def _set_status(self, msg, is_error):
        self._status_text.setText(msg)
        set_color(self._status_text, C.RED if is_error else C.TEXT2)
        self._status_dot.setStyleSheet(f"background: {C.RED if is_error else C.GREEN}; border-radius: 4px;")

    # ---------------- 1. Sayfa: FAN KONTROL ----------------
    def _build_fan_page(self):
        page = QWidget()
        v = QVBoxLayout(page)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(12)

        # Extreme Cooling karti
        self._hero = Card()
        hv = QVBoxLayout(self._hero)
        hv.setContentsMargins(18, 16, 18, 14)
        hv.setSpacing(12)
        top = QHBoxLayout()
        top.setSpacing(14)
        top.addWidget(IconBox("bolt", C.TEXT, 52, 24, hero=True), 0, Qt.AlignTop)
        tv = QVBoxLayout()
        tv.setSpacing(3)
        tv.addWidget(label("EXTREME COOLING", C.TEXT, 16, QFont.Bold))
        self._hero_desc = label(T("hero.desc.manual"), C.TEXT2, 12, wrap=True)
        tv.addWidget(self._hero_desc)
        tv.addWidget(label(T("hero.hotkey"), C.MUTED, 10))
        top.addLayout(tv, 1)
        self._extreme_switch = ModernSwitch()
        self._extreme_switch.toggled.connect(self._toggle_cooling)
        top.addWidget(self._extreme_switch, 0, Qt.AlignTop)
        hv.addLayout(top)
        strip = Card(bg=C.SUBCARD, radius=6)
        sl = QHBoxLayout(strip)
        sl.setContentsMargins(12, 7, 12, 7)
        self._hero_status = label(T("hero.status.off"), C.TEXT2, 12, QFont.DemiBold)
        sl.addWidget(self._hero_status)
        hv.addWidget(strip)
        v.addWidget(self._hero)

        # Otomatik fan karti
        auto = Card()
        av = QVBoxLayout(auto)
        av.setContentsMargins(18, 16, 18, 14)
        av.setSpacing(10)
        top = QHBoxLayout()
        top.setSpacing(14)
        top.addWidget(_OrangeBox("thermo"), 0, Qt.AlignTop)
        tv = QVBoxLayout()
        tv.setSpacing(3)
        tv.addWidget(label(T("auto.title"), C.TEXT, 14, QFont.Bold))
        tv.addWidget(label(T("auto.desc"), C.TEXT2, 12, wrap=True))
        top.addLayout(tv, 1)
        self._auto_switch = ModernSwitch()
        self._auto_switch.toggled.connect(self._toggle_auto)
        top.addWidget(self._auto_switch, 0, Qt.AlignTop)
        av.addLayout(top)
        line = QFrame()
        line.setFixedHeight(1)
        line.setStyleSheet("background: rgba(255,255,255,34);")
        av.addWidget(line)

        grid = QGridLayout()
        grid.setHorizontalSpacing(16)
        grid.setVerticalSpacing(4)
        grid.addWidget(label(T("auto.slider"), C.TEXT2, 12, QFont.DemiBold), 0, 0, 1, 3)
        self._slider = QSlider(Qt.Horizontal)
        self._slider.setRange(THRESHOLD_MIN, THRESHOLD_MAX)
        self._slider.setValue(THRESHOLD_DEFAULT)
        self._slider.setStyleSheet(SLIDER_QSS)
        self._slider.setCursor(Qt.PointingHandCursor)
        self._slider.valueChanged.connect(self._on_slider)
        self._slider.sliderPressed.connect(lambda: setattr(self, "_slider_busy", True))
        self._slider.sliderReleased.connect(self._on_slider_released)
        grid.addWidget(self._slider, 1, 0, 1, 3)
        grid.addWidget(label(f"{THRESHOLD_MIN} °C", C.MUTED, 10), 2, 0, Qt.AlignLeft)
        grid.addWidget(label(T("auto.recommended"), C.MUTED, 10), 2, 1, Qt.AlignHCenter)
        grid.addWidget(label(f"{THRESHOLD_MAX} °C", C.MUTED, 10), 2, 2, Qt.AlignRight)
        value_box = Card(bg=C.SUBCARD, radius=8)
        value_box.setFixedSize(94, 50)
        vb = QHBoxLayout(value_box)
        vb.setContentsMargins(0, 0, 0, 0)
        self._threshold_label = label(f"{THRESHOLD_DEFAULT} °C", C.RED_GLOW, 22, QFont.Black)
        self._threshold_label.setAlignment(Qt.AlignCenter)
        vb.addWidget(self._threshold_label)
        grid.addWidget(value_box, 0, 3, 3, 1, Qt.AlignVCenter)
        grid.setColumnStretch(1, 1)
        av.addLayout(grid)
        av.addWidget(label(T("auto.note"), C.MUTED, 11, wrap=True))
        v.addWidget(auto)

        # Fan kartlari
        fans = QHBoxLayout()
        fans.setSpacing(16)
        self._fan_widgets = []
        for title, sub in (("FAN 1 (CPU)", T("fan1.sub")), ("FAN 2 (GPU)", T("fan2.sub"))):
            card = Card()
            cv = QVBoxLayout(card)
            cv.setContentsMargins(18, 14, 18, 16)
            cv.setSpacing(2)
            head = QHBoxLayout()
            hl = QVBoxLayout()
            hl.setSpacing(1)
            hl.addWidget(label(title, C.TEXT, 13, QFont.Bold))
            hl.addWidget(label(sub, C.MUTED, 10))
            head.addLayout(hl, 1)
            pct = label("0%", C.TEXT2, 12, QFont.Bold)
            head.addWidget(pct, 0, Qt.AlignTop)
            cv.addLayout(head)
            rpm = label("— RPM", C.TEXT, 26, QFont.Black)
            cv.addWidget(rpm)
            bar = FanBar()
            cv.addSpacing(4)
            cv.addWidget(bar)
            self._fan_widgets.append((rpm, pct, bar))
            fans.addWidget(card, 1)
        v.addLayout(fans)

        # Sicaklik kartlari
        temps = QHBoxLayout()
        temps.setSpacing(12)
        self._temp_widgets = {}
        for key, ic, color, sub in (("cpu", "bolt", C.CYAN, "—"), ("gpu", "gamepad", C.GREEN, "—"),
                                    ("ir", "thermo", C.ORANGE, T("temp.ir.sub"))):
            card = Card()
            cv = QVBoxLayout(card)
            cv.setContentsMargins(14, 12, 14, 12)
            cv.setSpacing(2)
            head = QHBoxLayout()
            head.setSpacing(8)
            head.addWidget(IconBox(ic, color, 24, 14))
            head.addWidget(label(T(f"temp.{key}"), C.TEXT2, 11, QFont.Bold), 1)
            cv.addLayout(head)
            val = label("—", C.TEXT2, 22, QFont.Black)
            cv.addSpacing(4)
            cv.addWidget(val)
            src = label(sub, C.MUTED, 10)
            cv.addWidget(src)
            self._temp_widgets[key] = (card, val, src)
            temps.addWidget(card, 1)
        v.addLayout(temps)
        return page

    # ---------------- 2. Sayfa: DONANIM ----------------
    def _build_hardware_page(self):
        page = QWidget()
        v = QVBoxLayout(page)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(10)
        v.addWidget(label(T("hw.title"), C.TEXT, 16, QFont.Bold))
        v.addSpacing(2)
        self._hw_box = QVBoxLayout()
        self._hw_box.setSpacing(10)
        v.addLayout(self._hw_box)
        return page

    def _info_card(self, icon_name, caption, main, sub=""):
        card = Card()
        h = QHBoxLayout(card)
        h.setContentsMargins(16, 14, 16, 14)
        h.setSpacing(16)
        h.addWidget(IconBox(icon_name, C.TEXT, 42, 20), 0, Qt.AlignVCenter)
        tv = QVBoxLayout()
        tv.setSpacing(3)
        tv.addWidget(label(caption, C.TEXT2, 11, QFont.Bold))
        tv.addWidget(label(main, C.TEXT, 14, QFont.DemiBold, wrap=True))
        if sub:
            tv.addWidget(label(sub, C.MUTED, 10, wrap=True))
        h.addLayout(tv, 1)
        return card

    def _render_hardware(self):
        while self._hw_box.count():
            w = self._hw_box.takeAt(0).widget()
            if w:
                w.deleteLater()
        info = self._hwinfo
        if info is None:
            card = Card()
            QVBoxLayout(card).addWidget(label(T("hw.scanning"), C.TEXT2, 13))
            card.layout().setContentsMargins(18, 14, 18, 14)
            self._hw_box.addWidget(card)
            return
        if "error" in info:
            card = Card()
            QVBoxLayout(card).addWidget(label(F("hw.error", info["error"]), C.RED, 13, wrap=True))
            card.layout().setContentsMargins(18, 14, 18, 14)
            self._hw_box.addWidget(card)
            return

        self._hw_box.addWidget(self._info_card(
            "bolt", T("hw.cpu"), info.get("cpu.name", "?"),
            F("hw.cpu.cores", info.get("cpu.cores", "?"), info.get("cpu.threads", "?"))))
        gpus = []
        if info.get("gpu.ext"):
            gpus.append(T("hw.gpu.ext") + info["gpu.ext"])
        if info.get("gpu.int"):
            gpus.append(T("hw.gpu.int") + info["gpu.int"])
        self._hw_box.addWidget(self._info_card("gamepad", T("hw.gpu"), "\n".join(gpus) or "—"))

        size_mb = int(info.get("ram.size_mb") or 0)
        if size_mb:
            ram = f"{size_mb // 1024} GB {info.get('ram.type', '')}".strip()
            sub = F("hw.ram.modules", info.get("ram.modules", "?"), info.get("ram.speed") or "?")
        else:
            ram = f"{round(int(info.get('ram.total_kb') or 0) / 1048576)} GB"
            sub = ""
        self._hw_box.addWidget(self._info_card("memory", T("hw.ram"), ram, sub))
        self._hw_box.addWidget(self._info_card(
            "laptop", T("hw.model"), info.get("model.name") or "—", F("hw.model.type", info.get("model.type", "?"))))

        date = info.get("bios.date", "")
        m = re.match(r"(\d{2})/(\d{2})/(\d{4})", date)
        if m:
            date = f"{m.group(2)}.{m.group(1)}.{m.group(3)}"
        gz_err, unsupported = info.get("gamezone.error", ""), info.get("gamezone.unsupported", "")
        if unsupported:
            gz_text = F("hw.gamezone.unsupported", unsupported)
        else:
            gz_text = F("hw.gamezone.fail", gz_err) if gz_err else T("hw.gamezone.ok")
        self._hw_box.addWidget(self._info_card(
            "gear", T("hw.bios"), " • ".join(filter(None, [info.get("bios.version"), date])), gz_text))

    def _load_hardware_info(self):
        def done(info, err):
            self._hwinfo = {"error": self._error_text(err)} if err else info
            self._render_hardware()
        self._client.call("GetHardwareInfo", callback=done)

    # ---------------- 3. Sayfa: SISTEM ARACLARI ----------------
    def _build_system_page(self):
        page = QWidget()
        v = QVBoxLayout(page)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(10)
        v.addWidget(label(T("sys.title"), C.TEXT, 16, QFont.Bold))
        v.addSpacing(2)

        def row(icon_name, title, desc):
            card = Card()
            h = QHBoxLayout(card)
            h.setContentsMargins(16, 14, 16, 14)
            h.setSpacing(16)
            h.addWidget(IconBox(icon_name, C.TEXT, 42, 20), 0, Qt.AlignVCenter)
            tv = QVBoxLayout()
            tv.setSpacing(3)
            tv.addWidget(label(title, C.TEXT, 14, QFont.Bold))
            tv.addWidget(label(desc, C.TEXT2, 11, wrap=True))
            h.addLayout(tv, 1)
            sw = ModernSwitch()
            h.addWidget(sw, 0, Qt.AlignVCenter)
            v.addWidget(card)
            return sw

        self._winkey_switch = row("square", T("sys.winkey.title"), T("sys.winkey.desc"))
        self._winkey_switch.toggled.connect(lambda on: self._set_lock("winkey", on))
        self._tp_switch = row("mouse", T("sys.tp.title"), T("sys.tp.desc"))
        self._tp_switch.toggled.connect(lambda on: self._set_lock("touchpad", on))
        self._startup_switch = row("rocket", T("sys.startup.title"), T("sys.startup.desc"))
        self._startup_switch.set_checked_quietly(os.path.exists(AUTOSTART_PATH), animate=False)
        self._startup_switch.toggled.connect(self._toggle_startup)
        return page

    def _refresh_locks(self):
        def done(locks, err):
            if not err:
                self._locks = locks
                self._apply_locks()
        self._client.call("GetLocks", callback=done)

    def _apply_locks(self):
        if self._locks:
            self._winkey_switch.set_checked_quietly(self._locks.get("winkey", False))
            self._tp_switch.set_checked_quietly(self._locks.get("touchpad", False))

    def _set_lock(self, which, on):
        def done(now, err):
            if err:
                self._set_status(self._error_text(err), True)
                return
            self._locks = dict(self._locks or {}, **{which: now})
            self._apply_locks()
        self._client.call("SetLock", which, on, callback=done)

    def _toggle_startup(self, on):
        try:
            if on:
                os.makedirs(os.path.dirname(AUTOSTART_PATH), exist_ok=True)
                with open(AUTOSTART_PATH, "w") as f:
                    f.write("[Desktop Entry]\nType=Application\nName=Legion Y520 Fan Control\n"
                            "Exec=legion-fan-control --tray\nIcon=legion-fan-control\n"
                            "X-GNOME-Autostart-enabled=true\nNoDisplay=true\n")
            elif os.path.exists(AUTOSTART_PATH):
                os.remove(AUTOSTART_PATH)
            self._startup_switch.set_checked_quietly(on)
            self._set_status(T("startup.on" if on else "startup.off"), False)
        except OSError as ex:
            self._set_status(F("startup.fail", ex), True)

    # ---------------- 4. Sayfa: HAKKINDA ----------------
    def _build_about_page(self):
        page = QWidget()
        v = QVBoxLayout(page)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(10)
        v.addWidget(label(T("about.title"), C.TEXT, 16, QFont.Bold))
        v.addSpacing(2)

        card = Card()
        h = QHBoxLayout(card)
        h.setContentsMargins(18, 14, 18, 14)
        h.setSpacing(16)
        logo = Card(bg="#3A0805", radius=12)
        logo.set_edge(QColor(226, 35, 26, 0x90))
        logo.setFixedSize(56, 56)
        ll = QVBoxLayout(logo)
        ll.setContentsMargins(8, 8, 8, 8)
        img = QLabel()
        img.setPixmap(QPixmap(os.path.join(HERE, "icon-on.png")).scaled(
            40, 40, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        ll.addWidget(img, 0, Qt.AlignCenter)
        h.addWidget(logo)
        tv = QVBoxLayout()
        tv.setSpacing(3)
        tv.addWidget(label(T("app.title"), C.TEXT, 16, QFont.Bold))
        tv.addWidget(label(F("about.version", VERSION), C.TEXT2, 11))
        tv.addWidget(label(T("about.desc"), C.MUTED, 11, wrap=True))
        h.addLayout(tv, 1)
        v.addWidget(card)

        card = Card()
        cv = QVBoxLayout(card)
        cv.setContentsMargins(18, 14, 18, 14)
        cv.setSpacing(4)
        cv.addWidget(label(T("about.dev"), C.TEXT2, 11, QFont.Bold))
        cv.addWidget(label("Fatih (fatih5228)", C.TEXT, 14, QFont.Bold))
        v.addWidget(card)

        card = Card()
        h = QHBoxLayout(card)
        h.setContentsMargins(18, 14, 18, 14)
        h.setSpacing(16)
        h.addWidget(IconBox("github", C.TEXT, 42, 20), 0, Qt.AlignVCenter)
        tv = QVBoxLayout()
        tv.setSpacing(3)
        tv.addWidget(label(T("about.gh"), C.TEXT2, 11, QFont.Bold))
        link = label(f'<a href="{REPO_URL}" style="color:{C.CYAN}; text-decoration:none;">{REPO_URL}</a>', C.CYAN, 13)
        link.setOpenExternalLinks(True)
        tv.addWidget(link)
        tv.addWidget(label(T("about.ghnote"), C.MUTED, 10))
        h.addLayout(tv, 1)
        v.addWidget(card)

        card = Card()
        cv = QVBoxLayout(card)
        cv.setContentsMargins(18, 14, 18, 14)
        cv.setSpacing(4)
        cv.addWidget(label(T("update.title"), C.TEXT2, 11, QFont.Bold))
        cv.addWidget(label(F("update.current", VERSION), C.TEXT, 14, QFont.Bold))
        check = label(f'<a href="#" style="color:{C.CYAN}; text-decoration:none;">{T("update.check")}</a>', C.CYAN, 12)
        check.linkActivated.connect(lambda _: self._check_updates(manual=True))
        cv.addWidget(check)
        self._update_status = label("", C.TEXT2, 11, wrap=True)
        self._update_status.setTextFormat(Qt.RichText)
        self._update_status.setOpenExternalLinks(True)
        self._update_status.hide()
        cv.addWidget(self._update_status)
        v.addWidget(card)
        return page

    # ---------------- Dil ----------------
    def _change_language(self, code):
        if code == lang.current:
            return
        lang.current = code
        save_settings()
        self._build_ui()
        self._retranslate_tray()

    # ---------------- Fan Kontrol & Polling ----------------
    def _error_text(self, err):
        kind = getattr(err, "kind", "error")
        if kind == "noservice":
            return T("status.noservice")
        if kind == "denied":
            return T("status.denied")
        if kind == "autolocked":
            return T("auto.locked")
        return F("status.error", err)

    def _poll(self):
        if self._polling:
            return
        self._polling = True

        def done(st, err):
            self._polling = False
            if err:
                self._state = {"connected": False, "service_error": self._error_text(err)}
            else:
                self._state = st
            self._apply_state()
        self._client.call("GetState", callback=done)

    def _apply_state(self):
        st = self._state
        if not st:
            return
        self._max_rpm = st.get("max_rpm") or self._max_rpm

        if "auto_mode" in st:
            self._apply_auto_mode(st["auto_mode"])
            if not self._slider_busy and not self._threshold_timer.isActive():
                self._slider.blockSignals(True)
                self._slider.setValue(st.get("threshold", THRESHOLD_DEFAULT))
                self._slider.blockSignals(False)
                self._threshold_label.setText(f"{self._slider.value()} °C")

        cpu = st.get("cpu_temp", -1)
        cpu = cpu if cpu is not None and cpu > 0 else None
        gpu = st.get("gpu_temp", -1)
        gpu = gpu if gpu is not None and gpu > 0 else None

        if not st.get("connected"):
            if st.get("unsupported"):
                msg = F("status.unsupported", st["unsupported"])
            else:
                msg = st.get("service_error") or F("status.connerror", st.get("error", "?"))
            self._set_status(msg, True)
            for rpm, pct, bar in self._fan_widgets:
                rpm.setText("— RPM")
                pct.setText("0%")
                bar.set_ratio(0)
            self._set_temp("ir", None)
            if "service_error" in st:
                self._set_temp("cpu", None)
                self._set_temp("gpu", None)
                self._update_tray_info(None, None, None)
                return
        else:
            self._apply_cooling(st.get("cooling", False))
            for (rpm_lb, pct_lb, bar), rpm in zip(self._fan_widgets, (st.get("fan1", 0), st.get("fan2", 0))):
                rpm_lb.setText(f"{rpm} RPM")
                ratio = rpm / self._max_rpm if self._max_rpm else 0
                pct_lb.setText(f"{min(100, round(ratio * 100))}%")
                bar.set_ratio(ratio)
            ec = st.get("ec_temp", 0)
            self._set_temp("ir", ec if ec > 0 else None)
            self._set_status(F("status.lastupdate", time.strftime("%H:%M:%S")), False)

        # Ekranda ve otomatik modda ayni CPU degeri: coretemp, yoksa EC sensoru
        ec = st.get("ec_temp", 0)
        if cpu is not None:
            self._set_temp("cpu", cpu, F("temp.cpu.src", st.get("cpu_src") or "CPU"))
        else:
            self._set_temp("cpu", ec if ec > 0 else None, T("temp.cpu.src.ec") if ec > 0 else "—")

        note = st.get("gpu_note", "")
        gpu_sub = st.get("gpu_name") or {"sleep": T("temp.gpu.sleep"), "error": T("temp.gpu.error"),
                                         "nogpu": T("temp.gpu.none")}.get(note, "—")
        self._set_temp("gpu", gpu, gpu_sub)
        fans = (st.get("fan1"), st.get("fan2")) if st.get("connected") else None
        self._update_tray_info(cpu if cpu is not None else (ec if ec > 0 else None), gpu, fans)

    def _set_temp(self, key, temp, sub=None):
        card, val, src = self._temp_widgets[key]
        if sub is not None:
            src.setText(sub)
        if temp is None or temp <= 0:
            val.setText("—")
            set_color(val, C.TEXT2)
            card.set_edge(None)
            return
        t = round(temp)
        val.setText(f"{t} °C")
        if t >= 80:
            set_color(val, C.RED_GLOW)
            card.set_edge(QColor(226, 35, 26, 0x60))
        elif t >= 65:
            set_color(val, C.ORANGE)
            card.set_edge(QColor(245, 158, 11, 0x40))
        else:
            set_color(val, C.CYAN)
            card.set_edge(None)

    def _apply_cooling(self, on):
        self._extreme_switch.set_checked_quietly(on)
        self._hero_status.setText(T("hero.status.on" if on else "hero.status.off"))
        set_color(self._hero_status, C.RED_GLOW if on else C.TEXT2)
        self._hero.set_edge(QColor(226, 35, 26, 0x80) if on else None)
        self._tray_toggle.setText(T("tray.cool.off" if on else "tray.cool.on"))
        self._tray.setIcon(self._icon_on if on else self._icon_off)

    def _apply_auto_mode(self, on):
        self._auto_switch.set_checked_quietly(on)
        self._extreme_switch.setEnabled(not on)
        self._extreme_switch.update()
        self._hero_desc.setText(T("hero.desc.auto" if on else "hero.desc.manual"))
        set_color(self._hero_desc, C.ORANGE if on else C.TEXT2)

    def _toggle_cooling(self, on):
        if self._state.get("auto_mode"):
            self._set_status(T("auto.locked"), False)
            return

        def done(now, err):
            if err:
                self._set_status(self._error_text(err), True)
                return
            self._state["cooling"] = now
            self._apply_cooling(now)
        self._client.call("SetExtremeCooling", on, callback=done)

    def _toggle_auto(self, on):
        def done(_, err):
            if err:
                self._set_status(self._error_text(err), True)
            self._poll()
        self._apply_auto_mode(on)
        self._state["auto_mode"] = on
        self._client.call("SetAutoMode", on, callback=done)

    def _on_slider(self, value):
        self._threshold_label.setText(f"{value} °C")
        self._threshold_timer.start()

    def _on_slider_released(self):
        self._slider_busy = False
        self._threshold_timer.start()

    def _send_threshold(self):
        def done(_, err):
            if err:
                self._set_status(self._error_text(err), True)
        self._client.call("SetThreshold", self._slider.value(), callback=done)

    # ---------------- Sistem Tepsisi ----------------
    def _build_tray(self):
        self._tray = QSystemTrayIcon(self._icon_off, self)
        menu = QMenu()
        menu.setStyleSheet(MENU_QSS)
        self._tray_cpu = QAction(self)
        self._tray_gpu = QAction(self)
        self._tray_fans = QAction(self)
        for a in (self._tray_cpu, self._tray_gpu, self._tray_fans):
            a.setEnabled(False)
            menu.addAction(a)
        menu.addSeparator()
        self._tray_show = menu.addAction("", self.show_window)
        self._tray_toggle = menu.addAction("", lambda: self._toggle_cooling(not self._extreme_switch.isChecked()))
        menu.addSeparator()
        self._tray_exit = menu.addAction("", self.real_exit)
        self._tray_menu = menu
        self._tray.setContextMenu(menu)
        self._tray.activated.connect(self._on_tray_activated)
        self._retranslate_tray()
        self._update_tray_info(None, None, None)
        self._tray.show()

    def _retranslate_tray(self):
        self._tray.setToolTip(T("tray.tip"))
        self._tray_show.setText(T("tray.show"))
        self._tray_exit.setText(T("tray.exit"))
        self._tray_toggle.setText(T("tray.cool.off" if self._extreme_switch.isChecked() else "tray.cool.on"))
        self._apply_state()

    def _on_tray_activated(self, reason):
        if reason in (QSystemTrayIcon.Trigger, QSystemTrayIcon.DoubleClick):
            if self.isVisible() and not self.isMinimized():
                self.hide()
            else:
                self.show_window()

    def _update_tray_info(self, cpu, gpu, fans):
        if not hasattr(self, "_tray_cpu"):
            return
        self._tray_cpu.setText(F("tray.info.cpu", fmt_temp(cpu)))
        self._tray_gpu.setText(F("tray.info.gpu", fmt_temp(gpu)))
        self._tray_fans.setText(F("tray.info.fans", f"{fans[0]} / {fans[1]} RPM" if fans else "—"))
        self._tray.setToolTip(f"{T('tray.tip')}\nCPU {fmt_temp(cpu)} • GPU {fmt_temp(gpu)}")

    # ---------------- Guncelleme Kontrolu ----------------
    def _check_updates(self, manual):
        if manual:
            self._update_status.setText(T("update.checking"))
            set_color(self._update_status, C.TEXT2)
            self._update_status.show()

        def work():
            tag, url, failed = "", "", False
            try:
                req = urllib.request.Request(RELEASES_API, headers={
                    "User-Agent": f"LegionFanControl/{VERSION}", "Accept": "application/vnd.github+json"})
                with urllib.request.urlopen(req, timeout=10) as r:
                    data = json.loads(r.read().decode())
                tag, url = data.get("tag_name", ""), data.get("html_url", REPO_URL + "/releases")
            except Exception:  # noqa: BLE001 - ag hatalari sessizce raporlanir
                failed = True
            self._update_done.emit(tag, url, failed, manual)
        threading.Thread(target=work, daemon=True).start()

    def _on_update_done(self, tag, url, failed, manual):
        if failed or not tag:
            if manual:
                self._update_status.setText(T("update.fail"))
                set_color(self._update_status, C.ORANGE)
            return
        if _is_newer(tag):
            self._update_tag, self._update_url = tag, url
            self._show_update_banner()
            if not manual:
                self._tray.showMessage(T("update.balloon.title"), F("update.available", tag), self._icon_on, 6000)
        elif manual:
            self._update_status.setText(F("update.latest", VERSION))
            set_color(self._update_status, C.GREEN)

    def _show_update_banner(self):
        if not self._update_tag:
            return
        a = f'<a href="{self._update_url}" style="color:#FFFFFF; font-weight:600;">{T("update.get")}</a>'
        self._banner_text.setText(f'{F("update.available", self._update_tag)}&nbsp;&nbsp;&nbsp;{a}')
        self._banner.setVisible(not self._banner_dismissed)
        self._update_status.setText(
            f'<a href="{self._update_url}" style="color:{C.CYAN};">{F("update.download", self._update_tag)}</a>')
        self._update_status.show()

    def _dismiss_banner(self):
        self._banner_dismissed = True
        self._banner.hide()



class _TitleBar(QFrame):
    """Pencereyi surukleme (Wayland'de startSystemMove gerekir)."""

    def __init__(self, window):
        super().__init__()
        self._window = window

    def mousePressEvent(self, e):
        if e.button() == Qt.LeftButton:
            self._window.windowHandle().startSystemMove()

    def mouseDoubleClickEvent(self, e):
        if e.button() == Qt.LeftButton:
            self._window.showNormal() if self._window.isMaximized() else self._window.showMaximized()


class _MenuItem(QFrame):
    """Sol menu ogesi: secilince kirmizi serit ve vurgulu arka plan."""

    clicked = Signal()

    def __init__(self, icon_name, text):
        super().__init__()
        self._icon_name = icon_name
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedHeight(44)
        self.setObjectName("menuitem")
        h = QHBoxLayout(self)
        h.setContentsMargins(0, 0, 10, 0)
        h.setSpacing(0)
        self._strip = QFrame()
        self._strip.setFixedSize(3, 20)
        h.addWidget(self._strip, 0, Qt.AlignVCenter)
        h.addSpacing(14)
        self._icon = QLabel()
        self._icon.setFixedSize(18, 18)
        h.addWidget(self._icon)
        h.addSpacing(12)
        self._text = label(text, C.TEXT2, 13, QFont.DemiBold)
        h.addWidget(self._text, 1)
        self._active = None
        self.set_active(False)

    def set_active(self, on):
        if on == self._active:
            return
        self._active = on
        self.setStyleSheet(f"#menuitem {{ background: {C.SEL if on else 'transparent'}; border-radius: 8px; }}"
                           + ("" if on else f"#menuitem:hover {{ background: rgba(31,36,48,140); }}"))
        self._strip.setStyleSheet(f"background: {C.RED if on else 'transparent'}; border-radius: 1px;")
        self._icon.setPixmap(icons.pixmap(self._icon_name, C.RED_GLOW if on else C.TEXT2, 18))
        set_color(self._text, C.TEXT if on else C.TEXT2)
        f = self._text.font()
        f.setWeight(QFont.Bold if on else QFont.DemiBold)
        self._text.setFont(f)

    def mouseReleaseEvent(self, e):
        if e.button() == Qt.LeftButton and self.rect().contains(e.position().toPoint()):
            self.clicked.emit()


class _OrangeBox(IconBox):
    """Otomatik mod simgesi: turuncu cerceveli kutu."""

    def __init__(self, name):
        super().__init__(name, C.ORANGE, 42, 20)

    def paintEvent(self, e):
        p = QPainter(self)
        p.setRenderHint(QPainter.Antialiasing)
        r = QRectF(self.rect()).adjusted(0.5, 0.5, -0.5, -0.5)
        p.setBrush(QColor(245, 158, 11, 0x18))
        p.setPen(QPen(QColor(245, 158, 11, 0x70), 1))
        p.drawRoundedRect(r, 10, 10)
        p.drawPixmap(11, 11, self._px)
