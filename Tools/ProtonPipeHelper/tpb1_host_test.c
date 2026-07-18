/* Native TPB1 host used by the Windows and Android/Winlator bridge lab.
 * It avoids managed named-pipe/socket scheduling in the test harness so the
 * exact x64/x86 Windows guest and pipehelper path is exercised end to end. */

#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>

#define TPB1_FIXED_SIZE 58
#define TPB1_SESSION_SIZE 16
#define TPB1_TOKEN_SIZE 32
#define TPB1_MAX_PIPE_NAME 128
#define STRESS_BYTES (1024u * 1024u)
#define STRESS_BUFFER_SIZE 4096u

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

static int fail(const char *message)
{
    fprintf(stderr, "tpb1host: %s\n", message);
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

static int receive_stress(SOCKET client)
{
    BYTE header[12];
    BYTE buffer[STRESS_BUFFER_SIZE];
    uint32_t seed;
    uint32_t state;
    uint32_t offset;

    if (!receive_all(client, header, sizeof(header)) ||
        memcmp(header, "TPS1", 4) != 0 ||
        read_u32_le(header + 4) != STRESS_BYTES)
        return 0;
    seed = read_u32_le(header + 8);
    if (seed == 0)
        return 0;
    state = seed;
    for (offset = 0; offset < STRESS_BYTES;) {
        int count = (int)(STRESS_BYTES - offset);
        if (count > (int)sizeof(buffer))
            count = sizeof(buffer);
        if (!receive_all(client, buffer, count))
            return 0;
        for (int index = 0; index < count; index++) {
            if (buffer[index] != next_stress_byte(&state))
                return 0;
        }
        offset += (uint32_t)count;
    }
    return 1;
}

static int send_response_and_stress(
    SOCKET client, int architecture, const BYTE *response, int response_length)
{
    const int header_length = 12;
    const int total_length = response_length + header_length + (int)STRESS_BYTES;
    BYTE *packet = (BYTE *)malloc((size_t)total_length);
    uint32_t seed = (uint32_t)GetTickCount() ^ (uint32_t)GetCurrentProcessId() ^
                    0x48535400u ^ (uint32_t)architecture;
    uint32_t state;
    int offset;

    if (!packet)
        return 0;
    if (seed == 0)
        seed = 1;
    memcpy(packet, response, (size_t)response_length);
    memcpy(packet + response_length, "TPS2", 4);
    write_u32_le(packet + response_length + 4, STRESS_BYTES);
    write_u32_le(packet + response_length + 8, seed);
    state = seed;
    offset = response_length + header_length;
    while (offset < total_length)
        packet[offset++] = next_stress_byte(&state);
    offset = send_all(client, packet, total_length);
    free(packet);
    return offset;
}

int main(int argc, char **argv)
{
    BYTE expected_session[TPB1_SESSION_SIZE];
    BYTE expected_token[TPB1_TOKEN_SIZE];
    BYTE fixed_header[TPB1_FIXED_SIZE];
    BYTE pipe_name[TPB1_MAX_PIPE_NAME];
    BYTE request[16];
    BYTE expected_request[16] = {
        'T', 'P', 'G', '1', 0, 0x10, 0x11, 0x12,
        0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a
    };
    BYTE response[16] = {
        'T', 'P', 'R', '1', 0, 0x20, 0x21, 0x22,
        0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a
    };
    const char *expected_pipe_name;
    size_t expected_pipe_name_length;
    int architecture;
    int expect_rejection;
    WSADATA wsa;
    SOCKET listener = INVALID_SOCKET;
    SOCKET client = INVALID_SOCKET;
    struct sockaddr_in address;
    int address_length;
    unsigned short port;
    int result = 1;

    if (argc != 6) {
        fprintf(stderr,
                "usage: tpb1host.exe <sessionHex> <tokenHex> <pipeName> <32|64> <accept|reject>\n");
        return 2;
    }
    expected_pipe_name = argv[3];
    expected_pipe_name_length = strlen(expected_pipe_name);
    architecture = atoi(argv[4]);
    expect_rejection = strcmp(argv[5], "reject") == 0;
    if (!parse_hex(argv[1], expected_session, sizeof(expected_session)) ||
        !parse_hex(argv[2], expected_token, sizeof(expected_token)) ||
        expected_pipe_name_length == 0 ||
        expected_pipe_name_length > TPB1_MAX_PIPE_NAME ||
        (architecture != 32 && architecture != 64) ||
        (!expect_rejection && strcmp(argv[5], "accept") != 0))
        return fail("invalid command line");

    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
        return fail("WSAStartup failed");

    listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listener == INVALID_SOCKET) {
        result = fail("listener socket creation failed");
        goto cleanup;
    }
    memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    address.sin_port = 0;
    if (bind(listener, (struct sockaddr *)&address, sizeof(address)) != 0 ||
        listen(listener, 1) != 0) {
        result = fail("listener bind/listen failed");
        goto cleanup;
    }
    address_length = sizeof(address);
    if (getsockname(listener, (struct sockaddr *)&address, &address_length) != 0) {
        result = fail("getsockname failed");
        goto cleanup;
    }
    port = ntohs(address.sin_port);
    printf("PORT=%u\n", port);
    fflush(stdout);

    expected_request[4] = (BYTE)architecture;
    response[4] = (BYTE)architecture;
    for (int phase = 0; phase < (expect_rejection ? 1 : 2); phase++) {
        client = accept(listener, NULL, NULL);
        if (client == INVALID_SOCKET) {
            result = fail("accept failed");
            goto cleanup;
        }
        {
            BOOL nodelay = TRUE;
            setsockopt(client, IPPROTO_TCP, TCP_NODELAY,
                       (const char *)&nodelay, sizeof(nodelay));
        }
        if (!receive_all(client, fixed_header, sizeof(fixed_header))) {
            result = fail("TPB1 fixed header was truncated");
            goto cleanup;
        }
        if (memcmp(fixed_header, "TPB1", 4) != 0 ||
            fixed_header[4] != 0 || fixed_header[5] != 1 ||
            fixed_header[6] != 0 || fixed_header[7] != 1 ||
            memcmp(fixed_header + 8, expected_session, sizeof(expected_session)) != 0) {
            result = fail("TPB1 structural/session validation failed");
            goto cleanup;
        }
        {
            unsigned int pipe_name_length =
                ((unsigned int)fixed_header[56] << 8) | fixed_header[57];
            if (pipe_name_length != expected_pipe_name_length ||
                !receive_all(client, pipe_name, (int)pipe_name_length) ||
                memcmp(pipe_name, expected_pipe_name, pipe_name_length) != 0) {
                result = fail("TPB1 pipe-name validation failed");
                goto cleanup;
            }
        }

        if (memcmp(fixed_header + 24, expected_token, sizeof(expected_token)) != 0) {
            if (!expect_rejection) {
                result = fail("TPB1 token validation failed");
                goto cleanup;
            }
            printf("WRONG_TOKEN_REJECTED=1\n");
            fflush(stdout);
            result = 0;
            goto cleanup;
        }
        if (expect_rejection) {
            result = fail("rejection fixture presented the accepted token");
            goto cleanup;
        }

        if (!send_all(client, (const BYTE *)"OKAY", 4) ||
            !receive_all(client, request, sizeof(request))) {
            result = fail("authenticated guest request failed");
            goto cleanup;
        }
        if (memcmp(request, expected_request, sizeof(request)) != 0) {
            result = fail("guest request vector mismatch");
            goto cleanup;
        }
        if (phase == 0) {
            if (!send_all(client, response, sizeof(response))) {
                result = fail("guest response send failed");
                goto cleanup;
            }
            if (!receive_stress(client)) {
                result = fail("guest-to-host randomized stress failed");
                goto cleanup;
            }
            printf("GUEST_STRESS_RECEIVED=1\n");
            fflush(stdout);
            closesocket(client);
            client = INVALID_SOCKET;
            continue;
        }
        if (!send_response_and_stress(
                client, architecture, response, sizeof(response))) {
            result = fail("host-to-guest randomized stress failed");
            goto cleanup;
        }
    }
    printf("TPB1_NATIVE_ROUND_TRIP=PASS\n");
    printf("TPB1_RECONNECT=PASS\n");
    printf("RANDOMIZED_BYTES_EACH_DIRECTION=%u\n", STRESS_BYTES);
    fflush(stdout);
    result = 0;

cleanup:
    if (client != INVALID_SOCKET)
        closesocket(client);
    if (listener != INVALID_SOCKET)
        closesocket(listener);
    WSACleanup();
    return result;
}
