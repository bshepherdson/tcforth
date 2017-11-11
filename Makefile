.PHONY: all
default: all

EMULATOR ?= dcpu

%.img: %.fs makedisk.py
	./makedisk.py $<
	mv disk.img $@

kernel.bin: kernel.asm
	dasm $<

all: kernel.bin core.img test.img

forth.rom: all bootstrap.dcs
	rm -f forth.rom
	touch forth.rom
	GODEBUG=cgocheck=0 $(EMULATOR) -turbo -disk core.img -script bootstrap.dcs kernel.bin

bootstrap: forth.rom FORCE

test: forth.rom FORCE
	GODEBUG=cgocheck=0 $(EMULATOR) -turbo -disk test.img -script test.dcs forth.rom

clean: FORCE
	rm -f kernel.bin core.img test.img forth.rom

FORCE:

