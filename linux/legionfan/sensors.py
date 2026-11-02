"""Sicaklik sensorleri ve donanim bilgisi (LibreHardwareMonitor + WMI yerine)."""

import glob
import os
import re
import subprocess


def _read(path, default=""):
    try:
        with open(path) as f:
            return f.read().strip()
    except OSError:
        return default


# ---------------- CPU: coretemp ----------------
def cpu_temp():
    """(sicaklik, sensor adi) dondurur; Package yoksa en sicak cekirdek."""
    best = None
    for hw in glob.glob("/sys/class/hwmon/hwmon*"):
        if _read(f"{hw}/name") not in ("coretemp", "k10temp"):
            continue
        for inp in glob.glob(f"{hw}/temp*_input"):
            label = _read(inp.replace("_input", "_label"), "CPU")
            try:
                t = int(_read(inp)) / 1000.0
            except ValueError:
                continue
            if label.startswith(("Package", "Tctl")):
                return t, "CPU Package"
            if best is None or t > best[0]:
                best = (t, label)
    return best if best else (None, "")


# ---------------- GPU: NVIDIA (hibrit) ----------------
def _nvidia_pci():
    for dev in glob.glob("/sys/bus/pci/devices/*"):
        if _read(f"{dev}/vendor") == "0x10de" and _read(f"{dev}/class").startswith("0x03"):
            return dev
    return None


def gpu_temp():
    """(sicaklik, ad, not) dondurur.

    Kart runtime-suspend durumundaysa nvidia-smi cagrilmaz: cagri karti uyandirir
    ve hibrit grafikte pil tuketimini artirir.
    """
    dev = _nvidia_pci()
    if dev is None:
        return None, "", "nogpu"
    if _read(f"{dev}/power/runtime_status") == "suspended":
        return None, "", "sleep"
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=temperature.gpu,name", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=3)
    except (OSError, subprocess.TimeoutExpired):
        return None, "", "error"
    if out.returncode != 0:
        return None, "", "error"
    temp, _, name = out.stdout.strip().partition(", ")
    try:
        return float(temp), name.strip(), ""
    except ValueError:
        return None, "", "error"


# ---------------- Donanim Bilgisi ----------------
def hardware_info():
    """Donanim sayfasi icin metin alanlari (root: dmidecode RAM modullerini okur)."""
    info = {}

    cpuinfo = _read("/proc/cpuinfo")
    m = re.search(r"^model name\s*:\s*(.+)$", cpuinfo, re.M)
    info["cpu.name"] = m.group(1).strip() if m else "?"
    cores = set(re.findall(r"^core id\s*:\s*(\d+)$", cpuinfo, re.M))
    info["cpu.cores"] = str(len(cores) or os.cpu_count() or 0)
    info["cpu.threads"] = str(os.cpu_count() or 0)

    ext, integ = [], []
    try:
        out = subprocess.run(["lspci", "-mm"], capture_output=True, text=True, timeout=5).stdout
        for line in out.splitlines():
            parts = re.findall(r'"([^"]*)"', line)
            if len(parts) >= 3 and parts[0] in ("VGA compatible controller", "3D controller", "Display controller"):
                vendor, model = parts[1], parts[2]
                name = re.search(r"\[(.+)\]", model)
                short = vendor.split()[0] + " " + (name.group(1) if name else model)
                (ext if vendor.startswith(("NVIDIA", "Advanced Micro")) else integ).append(short)
    except (OSError, subprocess.TimeoutExpired):
        pass
    info["gpu.ext"] = ", ".join(ext)
    info["gpu.int"] = ", ".join(integ)

    m = re.search(r"^MemTotal:\s*(\d+)", _read("/proc/meminfo"), re.M)
    info["ram.total_kb"] = m.group(1) if m else "0"
    sizes, speeds, kinds = [], [], []
    try:
        out = subprocess.run(["dmidecode", "-t", "17"], capture_output=True, text=True, timeout=5).stdout
        for block in out.split("\n\n"):
            # dmidecode 3.7+: "16 GiB", eskiler: "16384 MB"
            sm = re.search(r"^\s*Size:\s*(\d+)\s*([GM])i?B", block, re.M)
            if not sm:
                continue
            sizes.append(int(sm.group(1)) * (1024 if sm.group(2) == "G" else 1))
            tm = re.search(r"^\s*Type:\s*(\S+)", block, re.M)
            if tm:
                kinds.append(tm.group(1))
            vm = re.search(r"^\s*(?:Configured Memory Speed|Speed):\s*(\d+)", block, re.M)
            if vm:
                speeds.append(int(vm.group(1)))
    except (OSError, subprocess.TimeoutExpired):
        pass
    info["ram.modules"] = str(len(sizes))
    info["ram.size_mb"] = str(sum(sizes))
    info["ram.type"] = kinds[0] if kinds else ""
    info["ram.speed"] = str(max(speeds)) if speeds else ""

    dmi = "/sys/class/dmi/id"
    # Lenovo urun adini (Lenovo Y520-15IKBN) product_version'da, makine tipini (80WK) product_name'de tutar
    info["model.name"] = _read(f"{dmi}/product_version") or _read(f"{dmi}/sys_vendor")
    info["model.type"] = _read(f"{dmi}/product_name")
    info["bios.version"] = _read(f"{dmi}/bios_version")
    info["bios.date"] = _read(f"{dmi}/bios_date")
    return info
