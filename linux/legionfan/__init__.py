"""Legion Y520 Fan Kontrol - Linux surumu."""

VERSION = "2.7.1"
REPO_URL = "https://github.com/fatih5228/LegionFanControl"

# Servis (root) ile arayuz arasindaki D-Bus adresi
BUS_NAME = "io.github.fatih5228.LegionFanControl"
OBJECT_PATH = "/io/github/fatih5228/LegionFanControl"
INTERFACE = BUS_NAME

THRESHOLD_MIN, THRESHOLD_MAX, THRESHOLD_DEFAULT = 50, 95, 70
HYSTERESIS = 5
