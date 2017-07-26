.PHONY: all
default: all

%.img: %.fs makedisk.py
	./makedisk.py $<
	mv disk.img $@

kernel.bin: kernel.asm
	dasm $<

all: kernel.bin core.img test.img

run: all FORCE
	GODEBUG=cgocheck=0 ~/dcpu/go/src/emulator/emulator -turbo -disk core.img kernel.bin

clean: FORCE
	rm -f kernel.bin core.img test.img

FORCE:

