"""Servisle (D-Bus) konusan istemci.

Tum cagrilar tek bir arka plan is parcaciginda calisir; servis yavas yanit
verse bile arayuz donmaz. Sonuclar ana is parcacigina sinyal ile doner.
"""

import dbus
from PySide6.QtCore import QObject, QThread, Signal, Slot

from .. import BUS_NAME, INTERFACE, OBJECT_PATH

TIMEOUT = 5  # saniye


class ServiceError(Exception):
    """kind: 'noservice' | 'denied' | 'autolocked' | 'error'"""

    def __init__(self, kind, msg=""):
        super().__init__(msg)
        self.kind = kind


def _py(v):
    # dbus turlerini (dbus.Int32, dbus.Dictionary...) sade Python turlerine cevirir
    if isinstance(v, dbus.Boolean):
        return bool(v)
    if isinstance(v, (dbus.Int16, dbus.Int32, dbus.Int64, dbus.UInt16, dbus.UInt32, dbus.UInt64, dbus.Byte)):
        return int(v)
    if isinstance(v, dbus.Double):
        return float(v)
    if isinstance(v, dbus.String):
        return str(v)
    if isinstance(v, dbus.Dictionary):
        return {str(k): _py(x) for k, x in v.items()}
    if isinstance(v, dbus.Array):
        return [_py(x) for x in v]
    return v


class _Worker(QObject):
    done = Signal(object, object, object)  # callback, sonuc, hata

    def __init__(self):
        super().__init__()
        self._iface = None

    def _proxy(self):
        if self._iface is None:
            bus = dbus.SystemBus()
            obj = bus.get_object(BUS_NAME, OBJECT_PATH, introspect=False)
            self._iface = dbus.Interface(obj, INTERFACE)
        return self._iface

    @Slot(str, object, object)
    def run(self, method, args, callback):
        try:
            result = _py(getattr(self._proxy(), method)(*args, timeout=TIMEOUT))
            self.done.emit(callback, result, None)
        except dbus.exceptions.DBusException as ex:
            name = ex.get_dbus_name() or ""
            if name.endswith(("ServiceUnknown", "NameHasNoOwner", "NoReply", "Disconnected")):
                kind = "noservice"
                self._iface = None
            elif name.endswith("AccessDenied"):
                kind = "denied"
            elif name.endswith("AutoModeLocked"):
                kind = "autolocked"
            else:
                kind = "error"
            self.done.emit(callback, None, ServiceError(kind, ex.get_dbus_message() or name))
        except Exception as ex:  # noqa: BLE001 - beklenmeyen hata arayuzde gosterilir
            self._iface = None
            self.done.emit(callback, None, ServiceError("error", str(ex)))


class Client(QObject):
    _request = Signal(str, object, object)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._thread = QThread(self)
        self._worker = _Worker()
        self._worker.moveToThread(self._thread)
        self._request.connect(self._worker.run)
        self._worker.done.connect(self._deliver)
        self._thread.start()

    def call(self, method, *args, callback=None):
        """callback(sonuc, hata) ana is parcaciginda cagrilir."""
        self._request.emit(method, args, callback)

    # Ornek metodu olmali: sinyal ana is parcacigindaki nesneye kuyrukla iletilir
    @Slot(object, object, object)
    def _deliver(self, callback, result, error):
        if callback is not None:
            callback(result, error)

    def stop(self):
        self._thread.quit()
        self._thread.wait(2000)
