.PHONY: all
default: all

EMULATOR=~/dcpu/go/src/emulator/emulator

%.img: %.fs makedisk.py
	./makedisk.py $<
	mv disk.img $@

kernel.bin: kernel.asm
	dasm $<

all: kernel.bin core.img test.img

run: all FORCE
	GODEBUG=cgocheck=0 $(EMULATOR) -turbo -disk core.img kernel.bin

preload.bin: all bootstrap.dcs
	rm -f preload.bin
	touch preload.bin
	GODEBUG=cgocheck=0 $(EMULATOR) -turbo -disk core.img -script bootstrap.dcs kernel.bin

bootstrap: preload.bin FORCE

test: preload.bin FORCE
	GODEBUG=cgocheck=0 $(EMULATOR) -turbo -disk test.img -script test.dcs preload.bin

clean: FORCE
	rm -f kernel.bin core.img test.img

FORCE:

