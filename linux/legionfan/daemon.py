"""legion-fan-control servisi (root).

Donanima tek erisen surec budur: fan/EC cagrilarini yapar, otomatik modu
yurutur ve arayuze D-Bus (sistem veriyolu) uzerinden durum sunar. Arayuz
kapali olsa da otomatik mod calismaya devam eder.
"""

import configparser
import os
import signal
import sys
import time

import dbus
import dbus.service
from dbus.mainloop.glib import DBusGMainLoop
from gi.repository import GLib

from . import (BUS_NAME, HYSTERESIS, INTERFACE, OBJECT_PATH,
               THRESHOLD_DEFAULT, THRESHOLD_MAX, THRESHOLD_MIN, sensors)
from .gamezone import GameZone, GameZoneError

STATE_DIR = os.environ.get("STATE_DIRECTORY", "/var/lib/legion-fan-control")
STATE_FILE = os.path.join(STATE_DIR, "state.ini")
POLL_SECONDS = 2
# Arayuz bu sure icinde durum istemediyse GPU sorgulanmaz (nvidia-smi pahali)
CLIENT_IDLE_SECONDS = 8


class FanService(dbus.service.Object):
    def __init__(self, bus):
        super().__init__(bus, OBJECT_PATH)
        self._gz = None
        self._hw_error = ""
        self._max_rpm = 4000
        self._auto_mode = False
        self._threshold = THRESHOLD_DEFAULT
        # Extreme Cooling'i otomatik mod mu acti (otomatik mod kapatilinca geri almak icin)
        self._auto_triggered = False
        self._last_client = 0.0
        self._hwinfo = None
        self._state = {}
        self._load_state()
        self._poll()
        GLib.timeout_add_seconds(POLL_SECONDS, self._poll)

    # ---------------- Kalici Ayarlar ----------------
    def _load_state(self):
        cp = configparser.ConfigParser()
        cp.read(STATE_FILE)
        s = cp["state"] if cp.has_section("state") else {}
        self._auto_mode = s.get("AutoMode", "0") == "1"
        try:
            self._threshold = self._clamp(int(s.get("Threshold", THRESHOLD_DEFAULT)))
        except ValueError:
            self._threshold = THRESHOLD_DEFAULT

    def _save_state(self):
        try:
            os.makedirs(STATE_DIR, exist_ok=True)
            tmp = STATE_FILE + ".tmp"
            with open(tmp, "w") as f:
                f.write(f"[state]\nAutoMode={int(self._auto_mode)}\nThreshold={self._threshold}\n")
            os.replace(tmp, STATE_FILE)
        except OSError as ex:
            print(f"Durum kaydedilemedi: {ex}", file=sys.stderr)

    @staticmethod
    def _clamp(t):
        return max(THRESHOLD_MIN, min(THRESHOLD_MAX, t))

    # ---------------- Donanim ----------------
    def _hw(self):
        if self._gz is None:
            try:
                self._gz = GameZone()
                m = self._gz.fan_max_speed()
                if 500 <= m <= 10000:
                    self._max_rpm = m
                self._hw_error = ""
            except (GameZoneError, OSError) as ex:
                self._hw_error = str(ex)
        return self._gz

    def _set_cooling(self, on):
        gz = self._hw()
        if gz is None:
            raise dbus.exceptions.DBusException(self._hw_error, name=INTERFACE + ".Error")
        gz.set_cooling(on)
        return gz.cooling()

    def _poll(self):
        st = {"connected": False, "error": "", "max_rpm": self._max_rpm}
        gz = self._hw()
        cpu, cpu_src = sensors.cpu_temp()
        st["cpu_temp"], st["cpu_src"] = (cpu if cpu is not None else -1.0), cpu_src
        try:
            if gz is None:
                raise GameZoneError(self._hw_error)
            fan1, fan2 = gz.fan1_speed(), gz.fan2_speed()
            cooling = gz.cooling()
            ec = gz.ec_temp()

            # Otomatik Mod Tetikleme (CPU yoksa EC sensoru referans alinir)
            ref = cpu if cpu is not None else (ec if ec > 0 else None)
            if self._auto_mode and ref is not None:
                if not cooling and ref >= self._threshold:
                    gz.set_cooling(True)
                    cooling = gz.cooling()
                    self._auto_triggered = cooling
                elif cooling and ref <= self._threshold - HYSTERESIS:
                    gz.set_cooling(False)
                    cooling = gz.cooling()
                    if not cooling:
                        self._auto_triggered = False

            st.update(connected=True, fan1=fan1, fan2=fan2, cooling=cooling, ec_temp=ec)
        except (GameZoneError, OSError) as ex:
            st["error"] = str(ex)
            self._gz = None

        if time.monotonic() - self._last_client < CLIENT_IDLE_SECONDS:
            gpu, gpu_name, gpu_note = sensors.gpu_temp()
            st.update(gpu_temp=gpu if gpu is not None else -1.0, gpu_name=gpu_name, gpu_note=gpu_note)
        elif self._state:
            for k in ("gpu_temp", "gpu_name", "gpu_note"):
                if k in self._state:
                    st[k] = self._state[k]

        st.update(auto_mode=self._auto_mode, threshold=self._threshold,
                  auto_triggered=self._auto_triggered)
        self._state = st
        return True

    # ---------------- D-Bus Arayuzu ----------------
    @dbus.service.method(INTERFACE, out_signature="a{sv}")
    def GetState(self):
        if time.monotonic() - self._last_client >= CLIENT_IDLE_SECONDS:
            # Arayuz yeni acildi: GPU degerini bekletmeden hemen oku
            self._last_client = time.monotonic()
            self._poll()
        self._last_client = time.monotonic()
        return dbus.Dictionary(self._state, signature="sv")

    @dbus.service.method(INTERFACE, out_signature="a{ss}")
    def GetHardwareInfo(self):
        if self._hwinfo is None:
            self._hwinfo = sensors.hardware_info()
        info = dict(self._hwinfo)
        info["gamezone.error"] = "" if self._hw() else self._hw_error
        return info

    @dbus.service.method(INTERFACE, in_signature="b", out_signature="b")
    def SetExtremeCooling(self, on):
        if self._auto_mode:
            raise dbus.exceptions.DBusException("auto mode is on", name=INTERFACE + ".AutoModeLocked")
        now = self._set_cooling(bool(on))
        self._auto_triggered = False
        self._poll()
        return now

    @dbus.service.method(INTERFACE, out_signature="b")
    def ToggleExtremeCooling(self):
        # Global kisayol (Ctrl+Alt+F) bunu cagirir
        gz = self._hw()
        return self.SetExtremeCooling(not (gz and gz.cooling()))

    @dbus.service.method(INTERFACE, in_signature="b")
    def SetAutoMode(self, on):
        on = bool(on)
        turned_off = self._auto_mode and not on
        self._auto_mode = on
        # Otomatik modun actigi Extreme Cooling, mod kapatilinca acik kalmasin
        if turned_off and self._auto_triggered:
            try:
                self._set_cooling(False)
            except (GameZoneError, OSError, dbus.exceptions.DBusException):
                pass
        if not on:
            self._auto_triggered = False
        self._save_state()
        self._poll()

    @dbus.service.method(INTERFACE, in_signature="i")
    def SetThreshold(self, t):
        self._threshold = self._clamp(int(t))
        self._save_state()
        self._poll()

    @dbus.service.method(INTERFACE, out_signature="a{sb}")
    def GetLocks(self):
        gz = self._hw()
        if gz is None:
            raise dbus.exceptions.DBusException(self._hw_error, name=INTERFACE + ".Error")
        return {"winkey": gz.winkey_lock(), "touchpad": gz.touchpad_lock()}

    @dbus.service.method(INTERFACE, in_signature="sb", out_signature="b")
    def SetLock(self, which, on):
        gz = self._hw()
        if gz is None:
            raise dbus.exceptions.DBusException(self._hw_error, name=INTERFACE + ".Error")
        if which == "winkey":
            gz.set_winkey_lock(bool(on))
            return gz.winkey_lock()
        if which == "touchpad":
            gz.set_touchpad_lock(bool(on))
            return gz.touchpad_lock()
        raise dbus.exceptions.DBusException(f"unknown lock: {which}", name=INTERFACE + ".Error")

    def shutdown(self):
        # Servis dururken otomatik modun actigi sogutmayi geri al
        if self._auto_triggered:
            try:
                self._set_cooling(False)
            except Exception:
                pass


def main():
    if os.geteuid() != 0:
        sys.exit("legion-fan-control-daemon root olarak calismali")
    DBusGMainLoop(set_as_default=True)
    bus = dbus.SystemBus()
    name = dbus.service.BusName(BUS_NAME, bus, do_not_queue=True)  # noqa: F841 (ad sahipligi)
    svc = FanService(bus)
    loop = GLib.MainLoop()

    def stop(*_):
        svc.shutdown()
        loop.quit()
        return False

    GLib.unix_signal_add(GLib.PRIORITY_HIGH, signal.SIGTERM, stop)
    GLib.unix_signal_add(GLib.PRIORITY_HIGH, signal.SIGINT, stop)
    loop.run()


if __name__ == "__main__":
    main()
