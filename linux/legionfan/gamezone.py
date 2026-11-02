"""LENOVO_GAMEZONE_DATA erisimi (acpi_call uzerinden).

Windows surumu WMI sinifini (root\\wmi:LENOVO_GAMEZONE_DATA) cagirir; WMI bu
cagrilari firmware'deki \\_SB.GZFD.WMAA ACPI metoduna yonlendirir. Linux'ta
ayni metodu acpi_call cekirdek modulu ile dogrudan cagiriyoruz.

Metot numaralari Y520-15IKBN (BIOS 4KCN45WW) DSDT'sinden cikarildi.
"""

import threading

ACPI_CALL = "/proc/acpi/call"
WMAA = r"\_SB.GZFD.WMAA"

M_EC_TEMP = 0x01        # EC CPUT alani (Windows'taki "IR" sicakligi)
M_FAN1_SPEED = 0x08     # FANS * 100
M_FAN2_SPEED = 0x09     # FA2S * 100
M_FAN_MAX_SPEED = 0x0A  # sabit: 3400
M_SET_COOLING = 0x0D    # EC komutu 0x59: 0x77 ac / 0x76 kapat
M_GET_COOLING = 0x14    # FCST bit 0
M_SET_WINKEY = 0x16     # EC komutu 0x59: 0x78 / 0x79  -> GDST deger 2
M_GET_WINKEY = 0x17
M_SET_TOUCHPAD = 0x19   # EC komutu 0x59: 0x7B / 0x7A  -> GDST deger 4
M_GET_TOUCHPAD = 0x1A


class GameZoneError(Exception):
    pass


class GameZone:
    def __init__(self, path=ACPI_CALL):
        self._path = path
        self._lock = threading.Lock()
        # Modul yuklu degilse dosya yoktur; erken ve anlasilir hata ver
        try:
            open(self._path, "rb").close()
        except OSError as ex:
            raise GameZoneError(f"{self._path} acilamadi (acpi_call yuklu mu?): {ex}") from ex

    def call(self, method, arg=0):
        # acpi_call yaz-sonra-oku calisir; iki cagri ic ice girmesin
        with self._lock:
            with open(self._path, "w") as f:
                f.write(f"{WMAA} 0 {method:#x} {arg:#x}")
            with open(self._path, "rb") as f:
                raw = f.read().rstrip(b"\0").decode(errors="replace").strip()
        if not raw.startswith("0x"):
            raise GameZoneError(f"WMAA {method:#x}: {raw or 'yanit yok'}")
        return int(raw, 16)

    def fan1_speed(self): return self.call(M_FAN1_SPEED)
    def fan2_speed(self): return self.call(M_FAN2_SPEED)
    def fan_max_speed(self): return self.call(M_FAN_MAX_SPEED)
    def ec_temp(self): return self.call(M_EC_TEMP)

    def cooling(self): return self.call(M_GET_COOLING) == 1
    def set_cooling(self, on): self.call(M_SET_COOLING, 1 if on else 0)

    def winkey_lock(self): return self.call(M_GET_WINKEY) == 1
    def set_winkey_lock(self, on): self.call(M_SET_WINKEY, 1 if on else 0)

    def touchpad_lock(self): return self.call(M_GET_TOUCHPAD) == 1
    def set_touchpad_lock(self, on): self.call(M_SET_TOUCHPAD, 1 if on else 0)
