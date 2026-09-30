using System.Collections.Generic;
using System.Globalization;

namespace LegionFanControl
{
    // ---------------- Coklu Dil Destegi ----------------
    // Yeni dil eklemek icin: asagidaki sozluklerden birini kopyalayip
    // yeni bir Dictionary olusturun ve T()/Available icine ekleyin.
    public static class Lang
    {
        public static readonly string[] Available = { "tr", "en" };

        public static string Current = Detect();

        private static string Detect()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
        }

        public static string T(string key)
        {
            string s;
            if (Current == "tr" && Tr.TryGetValue(key, out s)) return s;
            if (En.TryGetValue(key, out s)) return s;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        // ---------------- Turkce ----------------
        private static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            { "app.title",          "LEGION Y520 — FAN KONTROL" },
            { "titlebar.sub",       "• FAN KONTROL" },

            { "menu.fan",           "Fan Kontrol" },
            { "menu.hw",            "Donanım" },
            { "menu.sys",           "Sistem Araçları" },
            { "menu.about",         "Hakkında" },

            { "switch.on",          "AÇIK" },
            { "switch.off",         "KAPALI" },

            { "status.ready",       "Sistem hazır" },
            { "status.lastupdate",  "Son güncelleme: {0}" },
            { "status.connerror",   "Bağlantı hatası: {0}" },
            { "status.error",       "Hata: {0}" },
            { "status.noconn",      "Bağlantı yok" },
            { "err.nogamezone",     "LENOVO_GAMEZONE_DATA bulunamadı" },
            { "err.noresponse",     "{0} yanıt vermedi" },
            { "err.lhm",            "LHM açılamadı: {0}" },
            { "err.schtasks",       "schtasks hata kodu: {0}" },

            { "hero.desc.manual",   "Maksimum fan devri (3700+ RPM) ile anında yüksek soğutma" },
            { "hero.desc.auto",     "Otomatik mod devrede • Manuel kontrol kilitlendi" },
            { "hero.badge.init",    "Durum: Standart Fan Hızı" },
            { "hero.status.on",     "● Extreme Cooling Aktif - Maksimum Fan Devri Devrede" },
            { "hero.status.off",    "○ Standart Fan Eğrisi Devrede" },
            { "hero.hotkey",        "Kısayol: Ctrl+Alt+F → Extreme Cooling aç/kapat" },
            { "hero.hotkey.fail",   "Ctrl+Alt+F kısayolu kaydedilemedi (başka bir uygulama kullanıyor)" },

            { "auto.title",         "OTOMATİK FAN KONTROLÜ" },
            { "auto.desc",          "İşlemci sıcaklığı eşiğe ulaştığında Extreme Cooling otomatik açılır" },
            { "auto.slider",        "Tetikleme Eşik Sıcaklığı" },
            { "auto.recommended",   "70 °C (Önerilen)" },
            { "auto.note",          "💡 Sıcaklık eşiğe ulaşınca Extreme Cooling açılır; sıcaklık eşiğin 5 °C altına inene kadar devrede kalır." },
            { "auto.locked",        "Otomatik mod etkinken manuel kontrol kapalı" },

            { "fan1.sub",           "Sol Fan • İşlemci Soğutması" },
            { "fan2.sub",           "Sağ Fan • Ekran Kartı Soğutması" },

            { "temp.cpu",           "İŞLEMCİ (CPU)" },
            { "temp.cpu.sub",       "Package Sensörü" },
            { "temp.gpu",           "EKRAN KARTI" },
            { "temp.ir",            "SİSTEM / IR" },
            { "temp.ir.sub",        "Anakart Sensörü" },

            { "hw.title",           "SİSTEM VE DONANIM BİLGİSİ" },
            { "hw.scanning",        "Donanım bilgileri taranıyor…" },
            { "hw.cpu",             "İŞLEMCİ (CPU)" },
            { "hw.cpu.extra",       "4 Çekirdek • Intel Kaby Lake" },
            { "hw.gpu",             "EKRAN KARTLARI (GPU)" },
            { "hw.gpu.ext",         "Harici: " },
            { "hw.gpu.int",         "Dahili: " },
            { "hw.ram",             "BELLEK (RAM)" },
            { "hw.ram.extra",       "Çift Kanal Sistem Belleği" },
            { "hw.model",           "MODEL & BİLGİSAYAR" },
            { "hw.model.extra",     "Lenovo Legion Y520 Serisi (Type 80WK)" },
            { "hw.bios",            "BIOS SÜRÜMÜ & WMI" },
            { "hw.bios.value",      "{0} • LENOVO_GAMEZONE: Aktif" },
            { "hw.bios.extra",      "WMI Fan & Oyun Bölgesi Desteği Doğrulandı" },
            { "hw.error",           "Donanım bilgisi alınamadı: {0}" },

            { "sys.title",          "SİSTEM VE OYUN ARAÇLARI" },
            { "sys.winkey.title",   "Windows Tuşu Kilidi (Oyun Modu)" },
            { "sys.winkey.desc",    "Oyun sırasında yanlışlıkla Windows tuşuna basıp masaüstüne dönmeyi engeller." },
            { "sys.tp.title",       "Touchpad (Dokunmatik Yüzey) Kilidi" },
            { "sys.tp.desc",        "Harici fare kullanırken veya oyun esnasında touchpad temaslarını önlemek için kilitler." },
            { "sys.startup.title",  "Windows ile Başlat" },
            { "sys.startup.desc",   "Bilgisayar açıldığında uygulama yönetici yetkisiyle otomatik başlar ve sistem tepsisinde çalışır." },
            { "startup.on",         "Windows ile başlatma açıldı" },
            { "startup.off",        "Windows ile başlatma kapatıldı" },
            { "startup.fail",       "Başlangıç ayarı değiştirilemedi: {0}" },

            { "about.title",        "HAKKINDA" },
            { "about.version",      "Sürüm {0} • .NET Framework (WPF)" },
            { "about.desc",         "Lenovo Legion Y520 için Extreme Cooling, otomatik fan kontrolü ve oyun araçları." },
            { "about.dev",          "GELİŞTİRİCİ" },
            { "about.gh",           "KAYNAK KOD & GÜNCELLEMELER" },
            { "about.ghnote",       "Hata bildirimi ve katkılar için GitHub deposunu ziyaret edebilirsiniz." },

            { "update.title",       "GÜNCELLEMELER" },
            { "update.current",     "Kurulu sürüm: v{0}" },
            { "update.check",       "Güncellemeleri denetle" },
            { "update.checking",    "Güncellemeler denetleniyor…" },
            { "update.latest",      "En yeni sürümü kullanıyorsunuz (v{0})" },
            { "update.available",   "Yeni sürüm mevcut: {0}" },
            { "update.download",    "{0} sürümünü indir" },
            { "update.get",         "İndir" },
            { "update.fail",        "Güncelleme denetimi başarısız — internet bağlantısını kontrol edin" },
            { "update.balloon.title", "Güncelleme Mevcut" },

            { "tray.tip",           "Legion Y520 Fan Kontrol" },
            { "tray.info.cpu",      "CPU: {0}" },
            { "tray.info.gpu",      "GPU: {0}" },
            { "tray.info.fans",     "Fanlar: {0}" },
            { "tray.show",          "Pencereyi Göster" },
            { "tray.cool.on",       "Extreme Cooling Aç" },
            { "tray.cool.off",      "Extreme Cooling Kapat" },
            { "tray.exit",          "Çıkış" },
        };

        // ---------------- English ----------------
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "app.title",          "LEGION Y520 — FAN CONTROL" },
            { "titlebar.sub",       "• FAN CONTROL" },

            { "menu.fan",           "Fan Control" },
            { "menu.hw",            "Hardware" },
            { "menu.sys",           "System Tools" },
            { "menu.about",         "About" },

            { "switch.on",          "ON" },
            { "switch.off",         "OFF" },

            { "status.ready",       "System ready" },
            { "status.lastupdate",  "Last update: {0}" },
            { "status.connerror",   "Connection error: {0}" },
            { "status.error",       "Error: {0}" },
            { "status.noconn",      "Not connected" },
            { "err.nogamezone",     "LENOVO_GAMEZONE_DATA not found" },
            { "err.noresponse",     "{0} did not respond" },
            { "err.lhm",            "Could not open LHM: {0}" },
            { "err.schtasks",       "schtasks error code: {0}" },

            { "hero.desc.manual",   "Instant high cooling with maximum fan speed (3700+ RPM)" },
            { "hero.desc.auto",     "Auto mode active • Manual control locked" },
            { "hero.badge.init",    "Status: Standard Fan Speed" },
            { "hero.status.on",     "● Extreme Cooling Active - Maximum Fan Speed Engaged" },
            { "hero.status.off",    "○ Standard Fan Curve Active" },
            { "hero.hotkey",        "Shortcut: Ctrl+Alt+F → Toggle Extreme Cooling" },
            { "hero.hotkey.fail",   "Could not register the Ctrl+Alt+F shortcut (used by another app)" },

            { "auto.title",         "AUTOMATIC FAN CONTROL" },
            { "auto.desc",          "Extreme Cooling turns on automatically when CPU temperature reaches the threshold" },
            { "auto.slider",        "Trigger Threshold Temperature" },
            { "auto.recommended",   "70 °C (Recommended)" },
            { "auto.note",          "💡 Extreme Cooling turns on when the threshold is reached and stays on until the temperature drops 5 °C below it." },
            { "auto.locked",        "Manual control is disabled while auto mode is on" },

            { "fan1.sub",           "Left Fan • CPU Cooling" },
            { "fan2.sub",           "Right Fan • GPU Cooling" },

            { "temp.cpu",           "PROCESSOR (CPU)" },
            { "temp.cpu.sub",       "Package Sensor" },
            { "temp.gpu",           "GRAPHICS CARD" },
            { "temp.ir",            "SYSTEM / IR" },
            { "temp.ir.sub",        "Motherboard Sensor" },

            { "hw.title",           "SYSTEM AND HARDWARE INFO" },
            { "hw.scanning",        "Scanning hardware info…" },
            { "hw.cpu",             "PROCESSOR (CPU)" },
            { "hw.cpu.extra",       "4 Cores • Intel Kaby Lake" },
            { "hw.gpu",             "GRAPHICS CARDS (GPU)" },
            { "hw.gpu.ext",         "Dedicated: " },
            { "hw.gpu.int",         "Integrated: " },
            { "hw.ram",             "MEMORY (RAM)" },
            { "hw.ram.extra",       "Dual Channel System Memory" },
            { "hw.model",           "MODEL & COMPUTER" },
            { "hw.model.extra",     "Lenovo Legion Y520 Series (Type 80WK)" },
            { "hw.bios",            "BIOS VERSION & WMI" },
            { "hw.bios.value",      "{0} • LENOVO_GAMEZONE: Active" },
            { "hw.bios.extra",      "WMI Fan & Game Zone Support Verified" },
            { "hw.error",           "Failed to get hardware info: {0}" },

            { "sys.title",          "SYSTEM AND GAMING TOOLS" },
            { "sys.winkey.title",   "Windows Key Lock (Game Mode)" },
            { "sys.winkey.desc",    "Prevents accidentally pressing the Windows key and dropping to the desktop during games." },
            { "sys.tp.title",       "Touchpad Lock" },
            { "sys.tp.desc",        "Locks the touchpad to prevent accidental touches while gaming or using an external mouse." },
            { "sys.startup.title",  "Start with Windows" },
            { "sys.startup.desc",   "The app starts automatically with administrator rights at logon and runs in the system tray." },
            { "startup.on",         "Start with Windows enabled" },
            { "startup.off",        "Start with Windows disabled" },
            { "startup.fail",       "Could not change startup setting: {0}" },

            { "about.title",        "ABOUT" },
            { "about.version",      "Version {0} • .NET Framework (WPF)" },
            { "about.desc",         "Extreme Cooling, automatic fan control and gaming tools for Lenovo Legion Y520." },
            { "about.dev",          "DEVELOPER" },
            { "about.gh",           "SOURCE CODE & UPDATES" },
            { "about.ghnote",       "Visit the GitHub repository for bug reports and contributions." },

            { "update.title",       "UPDATES" },
            { "update.current",     "Installed version: v{0}" },
            { "update.check",       "Check for updates" },
            { "update.checking",    "Checking for updates…" },
            { "update.latest",      "You are running the latest version (v{0})" },
            { "update.available",   "New version available: {0}" },
            { "update.download",    "Download {0}" },
            { "update.get",         "Download" },
            { "update.fail",        "Update check failed — check your internet connection" },
            { "update.balloon.title", "Update Available" },

            { "tray.tip",           "Legion Y520 Fan Control" },
            { "tray.info.cpu",      "CPU: {0}" },
            { "tray.info.gpu",      "GPU: {0}" },
            { "tray.info.fans",     "Fans: {0}" },
            { "tray.show",          "Show Window" },
            { "tray.cool.on",       "Turn Extreme Cooling On" },
            { "tray.cool.off",      "Turn Extreme Cooling Off" },
            { "tray.exit",          "Exit" },
        };
    }
}
