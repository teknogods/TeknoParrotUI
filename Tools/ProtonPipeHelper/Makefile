# TeknoParrot Proton pipe helper
# Cross-compiled from Linux with mingw-w64.
#
#   make            - build both 32-bit and 64-bit helpers
#   make pipehelper.exe     - 64-bit only
#   make pipehelper32.exe   - 32-bit only (needed for 32-bit Wine prefixes)

CC64 = x86_64-w64-mingw32-gcc
CC32 = i686-w64-mingw32-gcc

all: pipehelper.exe pipehelper32.exe

pipehelper.exe: pipehelper.c
	$(CC64) -O2 -Wall -Wextra -o $@ $< -lws2_32 -static

pipehelper32.exe: pipehelper.c
	$(CC32) -O2 -Wall -Wextra -o $@ $< -lws2_32 -static

clean:
	rm -f pipehelper.exe pipehelper32.exe

.PHONY: all clean
