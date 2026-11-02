"""Coklu dil destegi (Lang.cs'in karsiligi).

Yeni dil eklemek icin: sozluklerden birini kopyalayip AVAILABLE'a ekleyin.
"""

import locale
import os

AVAILABLE = ("tr", "en")


def _detect():
    lang = os.environ.get("LANG") or (locale.getlocale()[0] or "")
    return "tr" if lang.lower().startswith("tr") else "en"


current = _detect()


def T(key):
    if current == "tr" and key in TR:
        return TR[key]
    return EN.get(key, key)


def F(key, *args):
    return T(key).format(*args)


# ---------------- Turkce ----------------
TR = {
    "app.title":          "LEGION Y520 — FAN KONTROL",
    "titlebar.sub":       "• FAN KONTROL",

    "menu.fan":           "Fan Kontrol",
    "menu.hw":            "Donanım",
    "menu.sys":           "Sistem Araçları",
    "menu.about":         "Hakkında",

    "switch.on":          "AÇIK",
    "switch.off":         "KAPALI",

    "status.ready":       "Sistem hazır",
    "status.lastupdate":  "Son güncelleme: {0}",
    "status.connerror":   "Bağlantı hatası: {0}",
    "status.error":       "Hata: {0}",
    "status.noservice":   "Servise ulaşılamıyor — legion-fan-control.service çalışıyor mu?",
    "status.denied":      "İzin yok — kullanıcınız wheel grubunda olmalı",

    "hero.desc.manual":   "Fanları maksimum devirde çalıştırarak anında yüksek soğutma",
    "hero.desc.auto":     "Otomatik mod devrede • Manuel kontrol kilitlendi",
    "hero.status.on":     "● Extreme Cooling Aktif - Maksimum Fan Devri Devrede",
    "hero.status.off":    "○ Standart Fan Eğrisi Devrede",
    "hero.hotkey":        "Kısayol: Ctrl+Alt+F → Extreme Cooling aç/kapat",

    "auto.title":         "OTOMATİK FAN KONTROLÜ",
    "auto.desc":          "İşlemci sıcaklığı eşiğe ulaştığında Extreme Cooling otomatik açılır",
    "auto.slider":        "Tetikleme Eşik Sıcaklığı",
    "auto.recommended":   "70 °C (Önerilen)",
    "auto.note":          "Sıcaklık eşiğe ulaşınca Extreme Cooling açılır; sıcaklık eşiğin 5 °C altına inene kadar devrede kalır. Arayüz kapalıyken de servis tarafından yürütülür.",
    "auto.locked":        "Otomatik mod etkinken manuel kontrol kapalı",

    "fan1.sub":           "Sol Fan • İşlemci Soğutması",
    "fan2.sub":           "Sağ Fan • Ekran Kartı Soğutması",

    "temp.cpu":           "İŞLEMCİ (CPU)",
    "temp.cpu.src":       "{0} • coretemp",
    "temp.cpu.src.ec":    "EC sensörü (ACPI)",
    "temp.gpu":           "EKRAN KARTI",
    "temp.gpu.sleep":     "Uykuda (hibrit grafik, güç tasarrufu)",
    "temp.gpu.error":     "Okunamadı (nvidia-smi)",
    "temp.gpu.none":      "Harici ekran kartı yok",
    "temp.ir":            "SİSTEM / IR",
    "temp.ir.sub":        "EC sensörü (ACPI)",

    "hw.title":           "SİSTEM VE DONANIM BİLGİSİ",
    "hw.scanning":        "Donanım bilgileri taranıyor…",
    "hw.cpu":             "İŞLEMCİ (CPU)",
    "hw.cpu.cores":       "{0} çekirdek • {1} iş parçacığı",
    "hw.gpu":             "EKRAN KARTLARI (GPU)",
    "hw.gpu.ext":         "Harici: ",
    "hw.gpu.int":         "Dahili: ",
    "hw.ram":             "BELLEK (RAM)",
    "hw.ram.modules":     "{0} modül • {1} MHz",
    "hw.model":           "MODEL & BİLGİSAYAR",
    "hw.model.type":      "Makine tipi: {0}",
    "hw.bios":            "BIOS SÜRÜMÜ & ACPI",
    "hw.gamezone.ok":     "GameZone (\\_SB.GZFD.WMAA) erişilebilir • fan kontrolü destekleniyor",
    "hw.gamezone.fail":   "GameZone erişilemiyor: {0}",
    "hw.error":           "Donanım bilgisi alınamadı: {0}",

    "sys.title":          "SİSTEM VE OYUN ARAÇLARI",
    "sys.winkey.title":   "Super (Windows) Tuşu Kilidi (Oyun Modu)",
    "sys.winkey.desc":    "Oyun sırasında yanlışlıkla Super tuşuna basıp uygulama menüsünü açmayı engeller.",
    "sys.tp.title":       "Touchpad (Dokunmatik Yüzey) Kilidi",
    "sys.tp.desc":        "Harici fare kullanırken veya oyun esnasında touchpad temaslarını önlemek için kilitler.",
    "sys.startup.title":  "Oturum Açılışında Başlat",
    "sys.startup.desc":   "Oturum açıldığında uygulama sistem tepsisinde başlar. Fan servisi ve otomatik mod bundan bağımsız olarak her zaman çalışır.",
    "startup.on":         "Oturum açılışında başlatma açıldı",
    "startup.off":        "Oturum açılışında başlatma kapatıldı",
    "startup.fail":       "Başlangıç ayarı değiştirilemedi: {0}",

    "about.title":        "HAKKINDA",
    "about.version":      "Sürüm {0} • Linux (Python + Qt)",
    "about.desc":         "Lenovo Legion Y520 için Extreme Cooling, otomatik fan kontrolü ve oyun araçları.",
    "about.dev":          "GELİŞTİRİCİ",
    "about.gh":           "KAYNAK KOD & GÜNCELLEMELER",
    "about.ghnote":       "Hata bildirimi ve katkılar için GitHub deposunu ziyaret edebilirsiniz.",

    "update.title":       "GÜNCELLEMELER",
    "update.current":     "Kurulu sürüm: v{0}",
    "update.check":       "Güncellemeleri denetle",
    "update.checking":    "Güncellemeler denetleniyor…",
    "update.latest":      "En yeni sürümü kullanıyorsunuz (v{0})",
    "update.available":   "Yeni sürüm mevcut: {0}",
    "update.download":    "{0} sürümünü indir",
    "update.get":         "İndir",
    "update.fail":        "Güncelleme denetimi başarısız — internet bağlantısını kontrol edin",
    "update.balloon.title": "Güncelleme Mevcut",

    "tray.tip":           "Legion Y520 Fan Kontrol",
    "tray.info.cpu":      "CPU: {0}",
    "tray.info.gpu":      "GPU: {0}",
    "tray.info.fans":     "Fanlar: {0}",
    "tray.show":          "Pencereyi Göster",
    "tray.cool.on":       "Extreme Cooling Aç",
    "tray.cool.off":      "Extreme Cooling Kapat",
    "tray.exit":          "Çıkış",
}

# ---------------- English ----------------
EN = {
    "app.title":          "LEGION Y520 — FAN CONTROL",
    "titlebar.sub":       "• FAN CONTROL",

    "menu.fan":           "Fan Control",
    "menu.hw":            "Hardware",
    "menu.sys":           "System Tools",
    "menu.about":         "About",

    "switch.on":          "ON",
    "switch.off":         "OFF",

    "status.ready":       "System ready",
    "status.lastupdate":  "Last update: {0}",
    "status.connerror":   "Connection error: {0}",
    "status.error":       "Error: {0}",
    "status.noservice":   "Service unreachable — is legion-fan-control.service running?",
    "status.denied":      "Permission denied — your user must be in the wheel group",

    "hero.desc.manual":   "Runs the fans at maximum speed for instant high cooling",
    "hero.desc.auto":     "Auto mode active • Manual control locked",
    "hero.status.on":     "● Extreme Cooling Active - Maximum Fan Speed Engaged",
    "hero.status.off":    "○ Standard Fan Curve Active",
    "hero.hotkey":        "Shortcut: Ctrl+Alt+F → Toggle Extreme Cooling",

    "auto.title":         "AUTOMATIC FAN CONTROL",
    "auto.desc":          "Extreme Cooling turns on automatically when CPU temperature reaches the threshold",
    "auto.slider":        "Trigger Threshold Temperature",
    "auto.recommended":   "70 °C (Recommended)",
    "auto.note":          "Extreme Cooling turns on when the threshold is reached and stays on until the temperature drops 5 °C below it. Runs in the service even when this window is closed.",
    "auto.locked":        "Manual control is disabled while auto mode is on",

    "fan1.sub":           "Left Fan • CPU Cooling",
    "fan2.sub":           "Right Fan • GPU Cooling",

    "temp.cpu":           "PROCESSOR (CPU)",
    "temp.cpu.src":       "{0} • coretemp",
    "temp.cpu.src.ec":    "EC sensor (ACPI)",
    "temp.gpu":           "GRAPHICS CARD",
    "temp.gpu.sleep":     "Sleeping (hybrid graphics, power saving)",
    "temp.gpu.error":     "Unavailable (nvidia-smi)",
    "temp.gpu.none":      "No dedicated GPU",
    "temp.ir":            "SYSTEM / IR",
    "temp.ir.sub":        "EC sensor (ACPI)",

    "hw.title":           "SYSTEM AND HARDWARE INFO",
    "hw.scanning":        "Scanning hardware info…",
    "hw.cpu":             "PROCESSOR (CPU)",
    "hw.cpu.cores":       "{0} cores • {1} threads",
    "hw.gpu":             "GRAPHICS CARDS (GPU)",
    "hw.gpu.ext":         "Dedicated: ",
    "hw.gpu.int":         "Integrated: ",
    "hw.ram":             "MEMORY (RAM)",
    "hw.ram.modules":     "{0} modules • {1} MHz",
    "hw.model":           "MODEL & COMPUTER",
    "hw.model.type":      "Machine type: {0}",
    "hw.bios":            "BIOS VERSION & ACPI",
    "hw.gamezone.ok":     "GameZone (\\_SB.GZFD.WMAA) available • fan control supported",
    "hw.gamezone.fail":   "GameZone unavailable: {0}",
    "hw.error":           "Failed to get hardware info: {0}",

    "sys.title":          "SYSTEM AND GAMING TOOLS",
    "sys.winkey.title":   "Super (Windows) Key Lock (Game Mode)",
    "sys.winkey.desc":    "Prevents accidentally pressing the Super key and opening the app launcher during games.",
    "sys.tp.title":       "Touchpad Lock",
    "sys.tp.desc":        "Locks the touchpad to prevent accidental touches while gaming or using an external mouse.",
    "sys.startup.title":  "Start at Login",
    "sys.startup.desc":   "The app starts in the system tray when you log in. The fan service and auto mode always run regardless.",
    "startup.on":         "Start at login enabled",
    "startup.off":        "Start at login disabled",
    "startup.fail":       "Could not change startup setting: {0}",

    "about.title":        "ABOUT",
    "about.version":      "Version {0} • Linux (Python + Qt)",
    "about.desc":         "Extreme Cooling, automatic fan control and gaming tools for Lenovo Legion Y520.",
    "about.dev":          "DEVELOPER",
    "about.gh":           "SOURCE CODE & UPDATES",
    "about.ghnote":       "Visit the GitHub repository for bug reports and contributions.",

    "update.title":       "UPDATES",
    "update.current":     "Installed version: v{0}",
    "update.check":       "Check for updates",
    "update.checking":    "Checking for updates…",
    "update.latest":      "You are running the latest version (v{0})",
    "update.available":   "New version available: {0}",
    "update.download":    "Download {0}",
    "update.get":         "Download",
    "update.fail":        "Update check failed — check your internet connection",
    "update.balloon.title": "Update Available",

    "tray.tip":           "Legion Y520 Fan Control",
    "tray.info.cpu":      "CPU: {0}",
    "tray.info.gpu":      "GPU: {0}",
    "tray.info.fans":     "Fans: {0}",
    "tray.show":          "Show Window",
    "tray.cool.on":       "Turn Extreme Cooling On",
    "tray.cool.off":      "Turn Extreme Cooling Off",
    "tray.exit":          "Exit",
}
