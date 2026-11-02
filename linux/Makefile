# Legion Y520 Fan Kontrol - kurulum (PKGBUILD'ler ve diger paketler bunu kullanir)
#   make DESTDIR=/paket/koku install
# Servis dosyasi /usr/lib/legion-fan-control yolunu kullandigi icin onek sabittir.

LIBDIR = /usr/lib/legion-fan-control

.PHONY: install
install:
	install -d $(DESTDIR)$(LIBDIR)/legionfan/gui
	install -m644 legionfan/*.py $(DESTDIR)$(LIBDIR)/legionfan/
	install -m644 legionfan/gui/*.py legionfan/gui/*.png $(DESTDIR)$(LIBDIR)/legionfan/gui/
	python3 -m compileall -q -d $(LIBDIR) $(DESTDIR)$(LIBDIR)
	install -Dm755 legion-fan-control $(DESTDIR)/usr/bin/legion-fan-control
	install -Dm644 data/legion-fan-control.service $(DESTDIR)/usr/lib/systemd/system/legion-fan-control.service
	install -Dm644 data/io.github.fatih5228.LegionFanControl.conf \
		$(DESTDIR)/usr/share/dbus-1/system.d/io.github.fatih5228.LegionFanControl.conf
	install -Dm644 data/acpi_call.conf $(DESTDIR)/usr/lib/modules-load.d/legion-fan-control.conf
	install -Dm644 data/legion-fan-control.desktop $(DESTDIR)/usr/share/applications/legion-fan-control.desktop
	install -Dm644 data/legion-fan-control-shortcuts.desktop \
		$(DESTDIR)/usr/share/kglobalaccel/legion-fan-control-shortcuts.desktop
	install -Dm644 legionfan/gui/icon-on.png $(DESTDIR)/usr/share/pixmaps/legion-fan-control.png
	install -Dm644 ../LICENSE $(DESTDIR)/usr/share/licenses/legion-fan-control/LICENSE
