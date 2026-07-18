/*
 * Minimal Windows guest used by the Android/Winlator bridge lab.
 *
 * It proves both directions of the existing bridge without depending on a
 * commercial game or OpenParrot:
 *   - opens pipehelper's Windows named pipe and exchanges deterministic bytes;
 *   - opens pipehelper's named mapping, verifies bytes owned by Android, and
 *     writes a marker that must be mirrored back to the rootfs page file.
 *
 * Build both x64 and x86 variants with build-winlator-bridge-guest.ps1.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>

#define HOST_PREFIX_SIZE 16
#define GUEST_MARKER_OFFSET 32
#define GUEST_MARKER_SIZE 16
#define ARCH_OFFSET 48
#define STRESS_BYTES (1024u * 1024u)
#define STRESS_BUFFER_SIZE 4096u

static int fail_last_error(const char *operation)
{
    fprintf(stderr, "bridgeguest: %s failed: %lu\n", operation, GetLastError());
    return 1;
}

static HANDLE open_mapping_with_retry(const char *mapping_name)
{
    DWORD started = GetTickCount();
    HANDLE mapping;

    do {
        mapping = OpenFileMappingA(FILE_MAP_ALL_ACCESS, FALSE, mapping_name);
        if (mapping)
            return mapping;
        Sleep(25);
    } while (GetTickCount() - started < 15000);

    return NULL;
}

static HANDLE open_pipe_with_retry(const char *pipe_path)
{
    DWORD started = GetTickCount();
    do {
        if (WaitNamedPipeA(pipe_path, 1000)) {
            HANDLE pipe = CreateFileA(pipe_path, GENERIC_READ | GENERIC_WRITE,
                                      0, NULL, OPEN_EXISTING,
                                      FILE_ATTRIBUTE_NORMAL, NULL);
            if (pipe != INVALID_HANDLE_VALUE)
                return pipe;
        }
        Sleep(25);
    } while (GetTickCount() - started < 15000);
    return INVALID_HANDLE_VALUE;
}

static int read_exact(HANDLE handle, BYTE *buffer, DWORD size)
{
    DWORD offset = 0;
    while (offset < size) {
        DWORD received = 0;
        if (!ReadFile(handle, buffer + offset, size - offset, &received, NULL) || received == 0)
            return 0;
        offset += received;
    }
    return 1;
}

static int write_all(HANDLE handle, const BYTE *buffer, DWORD size)
{
    DWORD offset = 0;
    while (offset < size) {
        DWORD written = 0;
        if (!WriteFile(handle, buffer + offset, size - offset, &written, NULL) ||
            written == 0)
            return 0;
        offset += written;
    }
    return 1;
}

static uint32_t read_u32_le(const BYTE *buffer)
{
    return (uint32_t)buffer[0] |
           ((uint32_t)buffer[1] << 8) |
           ((uint32_t)buffer[2] << 16) |
           ((uint32_t)buffer[3] << 24);
}

static void write_u32_le(BYTE *buffer, uint32_t value)
{
    buffer[0] = (BYTE)(value & 0xffu);
    buffer[1] = (BYTE)((value >> 8) & 0xffu);
    buffer[2] = (BYTE)((value >> 16) & 0xffu);
    buffer[3] = (BYTE)((value >> 24) & 0xffu);
}

static BYTE next_stress_byte(uint32_t *state)
{
    *state ^= *state << 13;
    *state ^= *state >> 17;
    *state ^= *state << 5;
    return (BYTE)(*state & 0xffu);
}

static int send_guest_stress(HANDLE pipe, BYTE architecture)
{
    BYTE header[12] = { 'T', 'P', 'S', '1', 0, 0, 0, 0, 0, 0, 0, 0 };
    BYTE buffer[STRESS_BUFFER_SIZE];
    uint32_t seed = (uint32_t)GetTickCount() ^ (uint32_t)GetCurrentProcessId() ^
                    0x47535400u ^ architecture;
    uint32_t state;
    uint32_t offset;

    if (seed == 0)
        seed = 1;
    write_u32_le(header + 4, STRESS_BYTES);
    write_u32_le(header + 8, seed);
    if (!write_all(pipe, header, sizeof(header)))
        return 0;

    state = seed;
    for (offset = 0; offset < STRESS_BYTES;) {
        DWORD count = (DWORD)(STRESS_BYTES - offset);
        if (count > sizeof(buffer))
            count = sizeof(buffer);
        for (DWORD index = 0; index < count; index++)
            buffer[index] = next_stress_byte(&state);
        if (!write_all(pipe, buffer, count))
            return 0;
        offset += count;
    }
    if (!FlushFileBuffers(pipe))
        return 0;
    fprintf(stdout, "PIPE_GUEST_TO_HOST_STRESS=PASS\n");
    fflush(stdout);
    return 1;
}

static int receive_host_stress(HANDLE pipe)
{
    BYTE header[12];
    BYTE buffer[STRESS_BUFFER_SIZE];
    uint32_t seed;
    uint32_t state;
    uint32_t offset;

    if (!read_exact(pipe, header, sizeof(header)) ||
        memcmp(header, "TPS2", 4) != 0 ||
        read_u32_le(header + 4) != STRESS_BYTES)
        return 0;
    seed = read_u32_le(header + 8);
    if (seed == 0)
        return 0;

    state = seed;
    for (offset = 0; offset < STRESS_BYTES;) {
        DWORD count = (DWORD)(STRESS_BYTES - offset);
        if (count > sizeof(buffer))
            count = sizeof(buffer);
        if (!read_exact(pipe, buffer, count))
            return 0;
        for (DWORD index = 0; index < count; index++) {
            if (buffer[index] != next_stress_byte(&state))
                return 0;
        }
        offset += count;
    }
    fprintf(stdout, "PIPE_HOST_TO_GUEST_STRESS=PASS\n");
    fflush(stdout);
    return 1;
}

int main(int argc, char **argv)
{
    char pipe_path[256];
    HANDLE mapping;
    volatile BYTE *page;
    HANDLE pipe;
    DWORD written = 0;
    const BYTE architecture = (BYTE)(sizeof(void *) * 8);
    BYTE request[16] = {
        'T', 'P', 'G', '1', 0, 0x10, 0x11, 0x12,
        0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a
    };
    BYTE expected_response[16] = {
        'T', 'P', 'R', '1', 0, 0x20, 0x21, 0x22,
        0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a
    };
    BYTE response[sizeof(expected_response)];
    int mapping_size;

    if (argc != 4) {
        fprintf(stderr, "usage: bridgeguest.exe <pipeName> <mappingName> <mappingSize>\n");
        return 2;
    }

    mapping_size = atoi(argv[3]);
    if (mapping_size <= ARCH_OFFSET) {
        fprintf(stderr, "bridgeguest: mapping is too small: %d\n", mapping_size);
        return 2;
    }

    mapping = open_mapping_with_retry(argv[2]);
    if (!mapping)
        return fail_last_error("OpenFileMapping");

    page = (volatile BYTE *)MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, mapping_size);
    if (!page)
        return fail_last_error("MapViewOfFile");

    for (int i = 0; i < HOST_PREFIX_SIZE; i++) {
        BYTE expected = (BYTE)(0xa0 + i);
        if (page[i] != expected) {
            fprintf(stderr,
                    "bridgeguest: host page mismatch at %d: expected 0x%02x, got 0x%02x\n",
                    i, expected, page[i]);
            return 3;
        }
    }
    fprintf(stdout, "SHARED_HOST_TO_GUEST=PASS\n");
    fflush(stdout);

    snprintf(pipe_path, sizeof(pipe_path), "\\\\.\\pipe\\%s", argv[1]);
    if (!WaitNamedPipeA(pipe_path, 15000))
        return fail_last_error("WaitNamedPipe");

    pipe = CreateFileA(pipe_path, GENERIC_READ | GENERIC_WRITE, 0, NULL,
                       OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (pipe == INVALID_HANDLE_VALUE)
        return fail_last_error("CreateFile(named pipe)");

    request[4] = architecture;
    expected_response[4] = architecture;
    if (!WriteFile(pipe, request, sizeof(request), &written, NULL) || written != sizeof(request))
        return fail_last_error("WriteFile(named pipe)");
    if (!read_exact(pipe, response, sizeof(response)))
        return fail_last_error("ReadFile(named pipe)");
    if (memcmp(response, expected_response, sizeof(response)) != 0) {
        fprintf(stderr, "bridgeguest: named-pipe response mismatch\n");
        return 4;
    }
    fprintf(stdout, "PIPE_ROUND_TRIP=PASS\n");
    if (!send_guest_stress(pipe, architecture)) {
        fprintf(stderr, "bridgeguest: guest-to-host randomized pipe stress failed\n");
        return 5;
    }
    CloseHandle(pipe);

    pipe = open_pipe_with_retry(pipe_path);
    if (pipe == INVALID_HANDLE_VALUE)
        return fail_last_error("ReconnectNamedPipe");
    written = 0;
    if (!WriteFile(pipe, request, sizeof(request), &written, NULL) || written != sizeof(request))
        return fail_last_error("WriteFile(reconnected named pipe)");
    if (!read_exact(pipe, response, sizeof(response)))
        return fail_last_error("ReadFile(reconnected named pipe)");
    if (memcmp(response, expected_response, sizeof(response)) != 0) {
        fprintf(stderr, "bridgeguest: reconnected named-pipe response mismatch\n");
        return 6;
    }
    fprintf(stdout, "PIPE_RECONNECT=PASS\n");
    if (!receive_host_stress(pipe)) {
        fprintf(stderr, "bridgeguest: host-to-guest randomized pipe stress failed\n");
        return 7;
    }
    fprintf(stdout, "PIPE_RANDOMIZED_BYTES_EACH_DIRECTION=%u\n", STRESS_BYTES);

    for (int i = 0; i < GUEST_MARKER_SIZE; i++)
        page[GUEST_MARKER_OFFSET + i] = (BYTE)(0xd0 + i);
    page[ARCH_OFFSET] = architecture;
    FlushViewOfFile((LPCVOID)page, mapping_size);
    Sleep(250);
    fprintf(stdout, "SHARED_GUEST_TO_HOST=PASS\nARCH=%u\nCOMPLETE=1\n", architecture);

    CloseHandle(pipe);
    UnmapViewOfFile((LPCVOID)page);
    CloseHandle(mapping);
    return 0;
}
