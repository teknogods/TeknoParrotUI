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
 *   pipehelper.exe pipe --name <pipeName> --host <host> --port <port>
 *     --session <32 hex> (--token <64 hex> | --token-env <environmentName>)
 *     [--shared-page <shmName> <shmSize> <hostShmPath>]
 *     [--ready-file <windowsPath>]
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
#define PIPE_BUFFER_SIZE (64 * 1024)
#define PIPE_WRITE_CHUNK 256
#define TPB1_FIXED_SIZE 58
#define TPB1_TOKEN_SIZE 32
#define TPB1_SESSION_SIZE 16
#define TPB1_MAX_PIPE_NAME 128

typedef struct {
    BYTE session[TPB1_SESSION_SIZE];
    BYTE token[TPB1_TOKEN_SIZE];
    const char *pipe_name;
} auth_ctx;

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
static int g_quiet;

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
    if (!g_quiet) {
        fprintf(stderr, "pipehelper: shm mirror active (%s <-> %s, %d bytes)%s\n",
                ctx->shm_name, ctx->host_path, ctx->shm_size,
                map_err == ERROR_ALREADY_EXISTS ? " [OPENED PRE-EXISTING MAPPING]" : " [created fresh]");
        fflush(stderr);
    }
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
                if (i == 0 && !g_quiet)
                    fprintf(stderr, "pipehelper: shm[0] host->wine %u->%u (tick %lu)\n",
                            prev[i], h, GetTickCount());
                wine_view[i] = h;
                prev[i] = h;
            } else if (w != prev[i]) {   /* game wrote (FFB, outputs)  */
                if (i == 0 && !g_quiet)
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

static int wait_for_pipe_io(HANDLE pipe, OVERLAPPED *overlapped, DWORD *transferred)
{
    if (WaitForSingleObject(overlapped->hEvent, INFINITE) != WAIT_OBJECT_0)
        return 0;
    return GetOverlappedResult(pipe, overlapped, transferred, FALSE) != FALSE;
}

static int connect_named_pipe(HANDLE pipe)
{
    OVERLAPPED overlapped;
    DWORD transferred = 0;
    int connected = 0;

    memset(&overlapped, 0, sizeof(overlapped));
    overlapped.hEvent = CreateEventA(NULL, TRUE, FALSE, NULL);
    if (!overlapped.hEvent)
        return 0;

    if (ConnectNamedPipe(pipe, &overlapped)) {
        connected = 1;
    } else {
        DWORD error = GetLastError();
        if (error == ERROR_PIPE_CONNECTED) {
            connected = 1;
        } else if (error == ERROR_IO_PENDING) {
            connected = wait_for_pipe_io(pipe, &overlapped, &transferred);
        }
    }
    CloseHandle(overlapped.hEvent);
    return connected;
}

/* game -> host: read from named pipe, send to socket */
static DWORD WINAPI pipe_to_sock(LPVOID param)
{
    bridge_ctx *ctx = (bridge_ctx *)param;
    char buf[BUF_SIZE];
    DWORD n;
    int first_chunk = 1;
    HANDLE io_event = CreateEventA(NULL, TRUE, FALSE, NULL);

    if (!io_event)
        goto out;

    for (;;) {
        OVERLAPPED overlapped;
        memset(&overlapped, 0, sizeof(overlapped));
        overlapped.hEvent = io_event;
        ResetEvent(io_event);
        n = 0;
        if (!ReadFile(ctx->pipe, buf, sizeof(buf), &n, &overlapped)) {
            if (GetLastError() != ERROR_IO_PENDING ||
                !wait_for_pipe_io(ctx->pipe, &overlapped, &n))
                break;
        }
        if (n == 0)
            break;
        DWORD off = 0;
        while (off < n) {
            int w = send(ctx->sock, buf + off, (int)(n - off), 0);
            if (w <= 0)
                goto out;
            off += (DWORD)w;
        }
        if (first_chunk) {
            if (!g_quiet) {
                fprintf(stderr, "pipehelper: first pipe-to-host chunk forwarded (%lu bytes)\n", n);
                fflush(stderr);
            }
            first_chunk = 0;
        }
    }
out:
    if (io_event)
        CloseHandle(io_event);
    InterlockedExchange(ctx->done, 1);
    return 0;
}

/* host -> game: read from socket, write to named pipe */
static DWORD WINAPI sock_to_pipe(LPVOID param)
{
    bridge_ctx *ctx = (bridge_ctx *)param;
    char buf[BUF_SIZE];
    int first_chunk = 1;
    HANDLE io_event = CreateEventA(NULL, TRUE, FALSE, NULL);

    if (!io_event)
        goto out;

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
            OVERLAPPED overlapped;
            DWORD w;
            DWORD chunk = (DWORD)n - off;
            if (chunk > PIPE_WRITE_CHUNK)
                chunk = PIPE_WRITE_CHUNK;
            memset(&overlapped, 0, sizeof(overlapped));
            overlapped.hEvent = io_event;
            ResetEvent(io_event);
            w = 0;
            if (!WriteFile(ctx->pipe, buf + off, chunk, &w, &overlapped) &&
                (GetLastError() != ERROR_IO_PENDING ||
                 !wait_for_pipe_io(ctx->pipe, &overlapped, &w))) {
                fprintf(stderr, "pipehelper: host-to-pipe write failed: %lu\n", GetLastError());
                fflush(stderr);
                goto out;
            }
            if (w == 0)
                goto out;
            off += w;
        }
        if (first_chunk) {
            if (!g_quiet) {
                fprintf(stderr, "pipehelper: first host-to-pipe chunk forwarded (%d bytes)\n", n);
                fflush(stderr);
            }
            first_chunk = 0;
        }
    }
out:
    if (io_event)
        CloseHandle(io_event);
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
    if (InetPtonA(AF_INET, host, &addr.sin_addr) != 1) {
        closesocket(s);
        return INVALID_SOCKET;
    }

    if (connect(s, (struct sockaddr *)&addr, sizeof(addr)) != 0) {
        closesocket(s);
        return INVALID_SOCKET;
    }

    /* low latency: input reports are tiny and frequent */
    BOOL nodelay = TRUE;
    setsockopt(s, IPPROTO_TCP, TCP_NODELAY, (const char *)&nodelay, sizeof(nodelay));
    return s;
}

static int send_all(SOCKET socket_handle, const BYTE *buffer, int length)
{
    int offset = 0;
    while (offset < length) {
        int count = send(socket_handle, (const char *)buffer + offset, length - offset, 0);
        if (count <= 0)
            return 0;
        offset += count;
    }
    return 1;
}

static int receive_all(SOCKET socket_handle, BYTE *buffer, int length)
{
    int offset = 0;
    while (offset < length) {
        int count = recv(socket_handle, (char *)buffer + offset, length - offset, 0);
        if (count <= 0)
            return 0;
        offset += count;
    }
    return 1;
}

static int authenticate_host(SOCKET socket_handle, const auth_ctx *auth)
{
    size_t pipe_name_length = strlen(auth->pipe_name);
    if (pipe_name_length == 0 || pipe_name_length > TPB1_MAX_PIPE_NAME)
        return 0;

    BYTE header[TPB1_FIXED_SIZE + TPB1_MAX_PIPE_NAME];
    memset(header, 0, sizeof(header));
    memcpy(header, "TPB1", 4);
    header[4] = 0;
    header[5] = 1; /* data protocol version */
    header[6] = 0;
    header[7] = 1; /* named-pipe channel */
    memcpy(header + 8, auth->session, TPB1_SESSION_SIZE);
    memcpy(header + 24, auth->token, TPB1_TOKEN_SIZE);
    header[56] = (BYTE)((pipe_name_length >> 8) & 0xff);
    header[57] = (BYTE)(pipe_name_length & 0xff);
    memcpy(header + TPB1_FIXED_SIZE, auth->pipe_name, pipe_name_length);

    BYTE acknowledgement[4];
    if (!send_all(socket_handle, header, TPB1_FIXED_SIZE + (int)pipe_name_length) ||
        !receive_all(socket_handle, acknowledgement, sizeof(acknowledgement)) ||
        memcmp(acknowledgement, "OKAY", sizeof(acknowledgement)) != 0) {
        fprintf(stderr, "pipehelper: TPB1 authentication rejected for pipe %s\n",
                auth->pipe_name);
        return 0;
    }

    if (!g_quiet) {
        fprintf(stderr, "pipehelper: TPB1 authenticated for pipe %s\n", auth->pipe_name);
        fflush(stderr);
    }
    return 1;
}

static int run_pipe_bridge(const char *pipe_name_arg, const char *host,
                           unsigned short port, const auth_ctx *auth,
                           const char *ready_file)
{
    char pipe_path[256];
    snprintf(pipe_path, sizeof(pipe_path), "\\\\.\\pipe\\%s", pipe_name_arg);

    /* Serve forever: when the game disconnects, recycle pipe + socket so the
     * host side can reconnect (mirrors runEmuOnly reconnect behavior). */
    for (;;) {
        HANDLE pipe = CreateNamedPipeA(
            pipe_path,
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,              /* one instance */
            PIPE_BUFFER_SIZE, PIPE_BUFFER_SIZE,
            0, NULL);
        if (pipe == INVALID_HANDLE_VALUE) {
            fprintf(stderr, "pipehelper: CreateNamedPipe(%s) failed: %lu\n",
                    pipe_path, GetLastError());
            return 1;
        }
        if (!g_quiet) {
            fprintf(stderr, "pipehelper: pipe %s created, waiting for game...\n", pipe_path);
            fflush(stderr);
        }

        /* A prepared launcher waits for this marker before it starts the
         * game.  That makes CreateFile(\\\\.\\pipe\\...) deterministic even
         * when Wine and OpenParrot are already warm. */
        if (ready_file) {
            HANDLE marker = CreateFileA(ready_file, GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, CREATE_ALWAYS,
                FILE_ATTRIBUTE_NORMAL, NULL);
            if (marker == INVALID_HANDLE_VALUE) {
                fprintf(stderr, "pipehelper: cannot create ready file %s: %lu\n",
                        ready_file, GetLastError());
                CloseHandle(pipe);
                return 1;
            }
            CloseHandle(marker);
        }

        /* Block until the game (OpenParrot) opens the pipe. */
        if (!connect_named_pipe(pipe)) {
            CloseHandle(pipe);
            continue;
        }
        if (!g_quiet) {
            fprintf(stderr, "pipehelper: game connected to pipe %s\n", pipe_path);
            fflush(stderr);
        }

        /* Game is connected - now attach to the host. */
        SOCKET sock = connect_host(host, port);
        if (sock == INVALID_SOCKET) {
            fprintf(stderr, "pipehelper: cannot reach host %s:%u\n", host, port);
            CloseHandle(pipe);
            Sleep(500);
            continue;
        }
        if (!g_quiet) {
            fprintf(stderr, "pipehelper: TCP attached to host %s:%u, bridging\n", host, port);
            fflush(stderr);
        }

        if (auth && !authenticate_host(sock, auth)) {
            closesocket(sock);
            DisconnectNamedPipe(pipe);
            CloseHandle(pipe);
            Sleep(500);
            continue;
        }

        volatile LONG done = 0;
        bridge_ctx ctx = { pipe, sock, &done };

        HANDLE threads[2];
        threads[0] = CreateThread(NULL, 0, pipe_to_sock, &ctx, 0, NULL);
        threads[1] = CreateThread(NULL, 0, sock_to_pipe, &ctx, 0, NULL);

        /* Wait until either direction breaks, then tear both down. */
        while (!InterlockedCompareExchange(&done, 0, 0))
            Sleep(50);

        closesocket(sock);
        CancelIoEx(pipe, NULL);
        DisconnectNamedPipe(pipe);
        if (!g_quiet) {
            fprintf(stderr, "pipehelper: bridge cycle ended, recycling pipe\n");
            fflush(stderr);
        }

        WaitForMultipleObjects(2, threads, TRUE, 2000);
        CloseHandle(threads[0]);
        CloseHandle(threads[1]);
        CloseHandle(pipe);
    }
}

/* ------------------------------------------------------------------ */

static int hex_nibble(char value)
{
    if (value >= '0' && value <= '9')
        return value - '0';
    if (value >= 'a' && value <= 'f')
        return value - 'a' + 10;
    if (value >= 'A' && value <= 'F')
        return value - 'A' + 10;
    return -1;
}

static int parse_hex(const char *value, BYTE *destination, int destination_size)
{
    if (!value || strlen(value) != (size_t)destination_size * 2)
        return 0;
    for (int index = 0; index < destination_size; index++) {
        int high = hex_nibble(value[index * 2]);
        int low = hex_nibble(value[index * 2 + 1]);
        if (high < 0 || low < 0)
            return 0;
        destination[index] = (BYTE)((high << 4) | low);
    }
    return 1;
}

static void print_usage(void)
{
    fprintf(stderr,
        "usage: pipehelper.exe <pipeName> <host> <port> [shmName shmSize hostShmPath]\n"
        "       pipehelper.exe pipe --name <pipeName> --host 127.0.0.1 --port <port>\n"
        "         --session <32 hex> (--token <64 hex> | --token-env <name>)\n"
        "         [--shared-page <shmName> <shmSize> <hostShmPath>]\n"
        "         [--ready-file <windowsPath>]\n"
        "         [--quiet]\n"
        "       pipehelper.exe shm <shmName> <shmSize> <hostShmPath>\n");
}

static int run_authenticated_mode(int argc, char **argv)
{
    const char *pipe_name = NULL;
    const char *host = NULL;
    const char *port_text = NULL;
    const char *session_hex = NULL;
    const char *token_hex = NULL;
    const char *token_environment = NULL;
    const char *shm_name = NULL;
    const char *shm_size_text = NULL;
    const char *shm_path = NULL;
    const char *ready_file = NULL;

    for (int index = 2; index < argc; index++) {
        if (strcmp(argv[index], "--quiet") == 0) {
            g_quiet = 1;
            continue;
        }
        if (strcmp(argv[index], "--shared-page") == 0) {
            if (index + 3 >= argc)
                return 0;
            shm_name = argv[++index];
            shm_size_text = argv[++index];
            shm_path = argv[++index];
            continue;
        }
        if (index + 1 >= argc)
            return 0;
        const char *value = argv[++index];
        if (strcmp(argv[index - 1], "--name") == 0)
            pipe_name = value;
        else if (strcmp(argv[index - 1], "--host") == 0)
            host = value;
        else if (strcmp(argv[index - 1], "--port") == 0)
            port_text = value;
        else if (strcmp(argv[index - 1], "--session") == 0)
            session_hex = value;
        else if (strcmp(argv[index - 1], "--token") == 0)
            token_hex = value;
        else if (strcmp(argv[index - 1], "--token-env") == 0)
            token_environment = value;
        else if (strcmp(argv[index - 1], "--ready-file") == 0)
            ready_file = value;
        else
            return 0;
    }

    if (token_environment) {
        if (token_hex || strlen(token_environment) == 0 ||
            strlen(token_environment) > 64) {
            fprintf(stderr, "pipehelper: invalid token environment argument\n");
            return 0;
        }
        for (const char *character = token_environment; *character; character++) {
            if (!((*character >= 'A' && *character <= 'Z') ||
                  (*character >= '0' && *character <= '9') ||
                  *character == '_')) {
                fprintf(stderr, "pipehelper: invalid token environment name\n");
                return 0;
            }
        }
        token_hex = getenv(token_environment);
    }

    char *port_end = NULL;
    long parsed_port = port_text ? strtol(port_text, &port_end, 10) : 0;
    auth_ctx auth;
    memset(&auth, 0, sizeof(auth));
    auth.pipe_name = pipe_name;
    if (!pipe_name || strlen(pipe_name) == 0 || strlen(pipe_name) > TPB1_MAX_PIPE_NAME ||
        !host || strcmp(host, "127.0.0.1") != 0 || !port_end || *port_end != '\0' ||
        parsed_port < 1 || parsed_port > 65535 ||
        !parse_hex(session_hex, auth.session, sizeof(auth.session)) ||
        !parse_hex(token_hex, auth.token, sizeof(auth.token))) {
        fprintf(stderr, "pipehelper: invalid authenticated pipe arguments\n");
        return 0;
    }

    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        fprintf(stderr, "pipehelper: WSAStartup failed\n");
        return 1;
    }

    if (shm_name && shm_size_text && shm_path) {
        static shm_ctx shm;
        shm.shm_name = shm_name;
        shm.shm_size = atoi(shm_size_text);
        shm.host_path = shm_path;
        if (shm.shm_size <= 0) {
            fprintf(stderr, "pipehelper: invalid shared-page size\n");
            WSACleanup();
            return 1;
        }
        CreateThread(NULL, 0, shm_mirror_thread, &shm, 0, NULL);
    }

    int result = run_pipe_bridge(
        pipe_name, host, (unsigned short)parsed_port, &auth, ready_file);
    WSACleanup();
    return result;
}

/* ------------------------------------------------------------------ */

int main(int argc, char **argv)
{
    if (argc < 4) {
        print_usage();
        return 1;
    }

    if (strcmp(argv[1], "pipe") == 0) {
        int result = run_authenticated_mode(argc, argv);
        if (result == 0)
            print_usage();
        return result == 0 ? 1 : result;
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

    int rc = run_pipe_bridge(pipe_name, host, port, NULL, NULL);
    WSACleanup();
    return rc;
}
