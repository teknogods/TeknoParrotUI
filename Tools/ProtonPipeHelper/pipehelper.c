/*
 * pipehelper - TeknoParrot Proton bridge helper
 *
 * Runs INSIDE the game's Wine/Proton prefix and bridges two things between
 * the native Linux TeknoParrotUI process and the Windows game:
 *
 * 1. Named pipe bridge (Sega Rally / Europa-R and other pipe-based games):
 *    Creates the real Windows named pipe (\\.\pipe\<name>) that OpenParrot
 *    connects to, and forwards all bytes over TCP loopback to the host.
 *
 *      TPUI (Linux) <=> TCP 127.0.0.1:<port> <=> pipehelper (Wine) <=> \\.\pipe\<name> <=> game
 *
 *    The TCP connection is only opened AFTER a pipe client (the game)
 *    connects, so the host's WaitForConnection() keeps its usual semantics.
 *
 * 2. Shared memory mirror (all game types - coins, FFB, JVS state):
 *    Creates the Windows named file mapping (e.g. "TeknoParrot_JvsState")
 *    that OpenParrot opens, and continuously mirrors it against the host's
 *    /dev/shm file (visible in Wine as Z:\dev\shm\...). Change detection is
 *    per byte in both directions.
 *
 * Usage:
 *   pipehelper.exe <pipeName> <host> <port> [shmName shmSize hostShmPath]
 *   pipehelper.exe shm <shmName> <shmSize> <hostShmPath>
 *
 * Examples:
 *   pipehelper.exe TeknoParrotPipe 127.0.0.1 43121 TeknoParrot_JvsState 64 "Z:\dev\shm\TeknoParrot_JvsState"
 *   pipehelper.exe shm TeknoParrot_JvsState 64 "Z:\dev\shm\TeknoParrot_JvsState"
 *
 * Build (Linux, mingw-w64): see Makefile in this directory.
 */

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define BUF_SIZE 4096

/* ------------------------------------------------------------------ */
/* Shared memory mirror                                               */
/* ------------------------------------------------------------------ */

typedef struct {
    const char *shm_name;
    int shm_size;
    const char *host_path;
} shm_ctx;

/*
 * JVS bus ordering guarantee: the sense byte (offset 0) is written by the
 * HOST emulator before it sends a JVS reply, but the polling mirror below
 * (Sleep(1)) can lose the race against the reply travelling through the
 * TCP->pipe bridge - a warm game checks the sense line microseconds after
 * reading the SETADDR reply, still sees the stale value, concludes another
 * board exists and assigns a phantom address 02 whose requests are never
 * answered (game dies with an I/O error on in-process relaunches, where
 * everything is JIT-warm and fast). sock_to_pipe copies the host-owned
 * sense byte SYNCHRONOUSLY before forwarding any host->game bytes, making
 * "sense before reply" deterministic. The byte is exclusively host-written
 * (0 on reset, 1 on address assignment), so this never clobbers game data.
 */
static volatile BYTE *g_sense_host_view;
static volatile BYTE *g_sense_wine_view;

static DWORD WINAPI shm_mirror_thread(LPVOID param)
{
    shm_ctx *ctx = (shm_ctx *)param;

    /* Windows named mapping - the game (OpenParrot) opens this by name. */
    HANDLE map = CreateFileMappingA(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE,
                                    0, ctx->shm_size, ctx->shm_name);
    DWORD map_err = GetLastError();
    if (!map) {
        fprintf(stderr, "pipehelper: CreateFileMapping(%s) failed: %lu\n",
                ctx->shm_name, map_err);
        return 1;
    }
    fprintf(stderr, "pipehelper: shm mirror active (%s <-> %s, %d bytes)%s\n",
            ctx->shm_name, ctx->host_path, ctx->shm_size,
            map_err == ERROR_ALREADY_EXISTS ? " [OPENED PRE-EXISTING MAPPING]" : " [created fresh]");
    fflush(stderr);
    volatile BYTE *wine_view = (volatile BYTE *)MapViewOfFile(
        map, FILE_MAP_ALL_ACCESS, 0, 0, ctx->shm_size);
    if (!wine_view) {
        fprintf(stderr, "pipehelper: MapViewOfFile(%s) failed: %lu\n",
                ctx->shm_name, GetLastError());
        return 1;
    }

    /* Host side - /dev/shm file exposed by Wine as Z:\dev\shm\... */
    HANDLE file = CreateFileA(ctx->host_path,
                              GENERIC_READ | GENERIC_WRITE,
                              FILE_SHARE_READ | FILE_SHARE_WRITE,
                              NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) {
        fprintf(stderr, "pipehelper: cannot open host shm file %s: %lu\n",
                ctx->host_path, GetLastError());
        return 1;
    }
    HANDLE fmap = CreateFileMappingA(file, NULL, PAGE_READWRITE,
                                     0, ctx->shm_size, NULL);
    if (!fmap) {
        fprintf(stderr, "pipehelper: CreateFileMapping(host file) failed: %lu\n",
                GetLastError());
        return 1;
    }
    volatile BYTE *host_view = (volatile BYTE *)MapViewOfFile(
        fmap, FILE_MAP_ALL_ACCESS, 0, 0, ctx->shm_size);
    if (!host_view) {
        fprintf(stderr, "pipehelper: MapViewOfFile(host file) failed: %lu\n",
                GetLastError());
        return 1;
    }

    BYTE *prev = (BYTE *)malloc(ctx->shm_size);
    if (!prev)
        return 1;

    /* Startup: host is the source of truth (TPUI created/owns the state). */
    for (int i = 0; i < ctx->shm_size; i++) {
        wine_view[i] = host_view[i];
        prev[i] = host_view[i];
    }

    /* Expose the views for the synchronous sense-byte propagation in
     * sock_to_pipe (see comment at the top of this section). */
    g_sense_host_view = host_view;
    g_sense_wine_view = wine_view;

    /* Per-byte change detection, both directions. Host wins conflicts. */
    for (;;) {
        for (int i = 0; i < ctx->shm_size; i++) {
            BYTE h = host_view[i];
            BYTE w = wine_view[i];
            if (h != prev[i]) {          /* host wrote (inputs, coins) */
                if (i == 0)
                    fprintf(stderr, "pipehelper: shm[0] host->wine %u->%u (tick %lu)\n",
                            prev[i], h, GetTickCount());
                wine_view[i] = h;
                prev[i] = h;
            } else if (w != prev[i]) {   /* game wrote (FFB, outputs)  */
                if (i == 0)
                    fprintf(stderr, "pipehelper: shm[0] wine->host %u->%u (tick %lu)\n",
                            prev[i], w, GetTickCount());
                host_view[i] = w;
                prev[i] = w;
            }
        }
        Sleep(1);
    }
}

/* ------------------------------------------------------------------ */
/* Named pipe <-> TCP bridge                                          */
/* ------------------------------------------------------------------ */

typedef struct {
    HANDLE pipe;
    SOCKET sock;
    volatile LONG *done;
} bridge_ctx;

/* game -> host: read from named pipe, send to socket */
static DWORD WINAPI pipe_to_sock(LPVOID param)
{
    bridge_ctx *ctx = (bridge_ctx *)param;
    char buf[BUF_SIZE];
    DWORD n;

    for (;;) {
        if (!ReadFile(ctx->pipe, buf, sizeof(buf), &n, NULL) || n == 0)
            break;
        DWORD off = 0;
        while (off < n) {
            int w = send(ctx->sock, buf + off, (int)(n - off), 0);
            if (w <= 0)
                goto out;
            off += (DWORD)w;
        }
    }
out:
    InterlockedExchange(ctx->done, 1);
    return 0;
}

/* host -> game: read from socket, write to named pipe */
static DWORD WINAPI sock_to_pipe(LPVOID param)
{
    bridge_ctx *ctx = (bridge_ctx *)param;
    char buf[BUF_SIZE];

    for (;;) {
        int n = recv(ctx->sock, buf, sizeof(buf), 0);
        if (n <= 0)
            break;
        /* JVS ordering: the sense byte must be visible to the game BEFORE
         * the reply that follows it (see g_sense_* comment above). */
        if (g_sense_host_view && g_sense_wine_view)
            g_sense_wine_view[0] = g_sense_host_view[0];
        DWORD off = 0;
        while (off < (DWORD)n) {
            DWORD w;
            if (!WriteFile(ctx->pipe, buf + off, (DWORD)n - off, &w, NULL))
                goto out;
            off += w;
        }
    }
out:
    InterlockedExchange(ctx->done, 1);
    return 0;
}

static SOCKET connect_host(const char *host, unsigned short port)
{
    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET)
        return INVALID_SOCKET;

    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = inet_addr(host);

    if (connect(s, (struct sockaddr *)&addr, sizeof(addr)) != 0) {
        closesocket(s);
        return INVALID_SOCKET;
    }

    /* low latency: input reports are tiny and frequent */
    BOOL nodelay = TRUE;
    setsockopt(s, IPPROTO_TCP, TCP_NODELAY, (const char *)&nodelay, sizeof(nodelay));
    return s;
}

static int run_pipe_bridge(const char *pipe_name_arg, const char *host,
                           unsigned short port)
{
    char pipe_path[256];
    snprintf(pipe_path, sizeof(pipe_path), "\\\\.\\pipe\\%s", pipe_name_arg);

    /* Serve forever: when the game disconnects, recycle pipe + socket so the
     * host side can reconnect (mirrors runEmuOnly reconnect behavior). */
    for (;;) {
        HANDLE pipe = CreateNamedPipeA(
            pipe_path,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,              /* one instance */
            BUF_SIZE, BUF_SIZE,
            0, NULL);
        if (pipe == INVALID_HANDLE_VALUE) {
            fprintf(stderr, "pipehelper: CreateNamedPipe(%s) failed: %lu\n",
                    pipe_path, GetLastError());
            return 1;
        }
        fprintf(stderr, "pipehelper: pipe %s created, waiting for game...\n", pipe_path);
        fflush(stderr);

        /* Block until the game (OpenParrot) opens the pipe. */
        if (!ConnectNamedPipe(pipe, NULL) &&
            GetLastError() != ERROR_PIPE_CONNECTED) {
            CloseHandle(pipe);
            continue;
        }
        fprintf(stderr, "pipehelper: game connected to pipe %s\n", pipe_path);
        fflush(stderr);

        /* Game is connected - now attach to the host. */
        SOCKET sock = connect_host(host, port);
        if (sock == INVALID_SOCKET) {
            fprintf(stderr, "pipehelper: cannot reach host %s:%u\n", host, port);
            CloseHandle(pipe);
            Sleep(500);
            continue;
        }
        fprintf(stderr, "pipehelper: TCP attached to host %s:%u, bridging\n", host, port);
        fflush(stderr);

        volatile LONG done = 0;
        bridge_ctx ctx = { pipe, sock, &done };

        HANDLE threads[2];
        threads[0] = CreateThread(NULL, 0, pipe_to_sock, &ctx, 0, NULL);
        threads[1] = CreateThread(NULL, 0, sock_to_pipe, &ctx, 0, NULL);

        /* Wait until either direction breaks, then tear both down. */
        while (!InterlockedCompareExchange(&done, 0, 0))
            Sleep(50);

        closesocket(sock);
        DisconnectNamedPipe(pipe);
        CloseHandle(pipe);
        fprintf(stderr, "pipehelper: bridge cycle ended, recycling pipe\n");
        fflush(stderr);

        WaitForMultipleObjects(2, threads, TRUE, 2000);
        CloseHandle(threads[0]);
        CloseHandle(threads[1]);
    }
}

/* ------------------------------------------------------------------ */

int main(int argc, char **argv)
{
    if (argc < 4) {
        fprintf(stderr,
            "usage: pipehelper.exe <pipeName> <host> <port> [shmName shmSize hostShmPath]\n"
            "       pipehelper.exe shm <shmName> <shmSize> <hostShmPath>\n");
        return 1;
    }

    /* shm-only mode: Type-X2 / Ex-Board games (COM port handled by PTY,
     * only the JVS state mapping needs mirroring). */
    if (strcmp(argv[1], "shm") == 0) {
        if (argc < 5) {
            fprintf(stderr, "usage: pipehelper.exe shm <shmName> <shmSize> <hostShmPath>\n");
            return 1;
        }
        shm_ctx shm = { argv[2], atoi(argv[3]), argv[4] };
        if (shm.shm_size <= 0) {
            fprintf(stderr, "pipehelper: invalid shm size\n");
            return 1;
        }
        return (int)shm_mirror_thread(&shm);
    }

    /* pipe bridge mode, with optional shm mirror */
    const char *pipe_name = argv[1];
    const char *host = argv[2];
    unsigned short port = (unsigned short)atoi(argv[3]);

    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        fprintf(stderr, "pipehelper: WSAStartup failed\n");
        return 1;
    }

    if (argc >= 7) {
        static shm_ctx shm;
        shm.shm_name = argv[4];
        shm.shm_size = atoi(argv[5]);
        shm.host_path = argv[6];
        if (shm.shm_size > 0)
            CreateThread(NULL, 0, shm_mirror_thread, &shm, 0, NULL);
    }

    int rc = run_pipe_bridge(pipe_name, host, port);
    WSACleanup();
    return rc;
}
