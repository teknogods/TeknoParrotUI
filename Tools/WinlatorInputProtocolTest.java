import com.winlator.teknoparrot.ForwardedInputProtocol;
import com.winlator.teknoparrot.ForwardedInputQueue;
import com.winlator.teknoparrot.ForwardedInputMapping;
import com.winlator.teknoparrot.ForwardedInputClient;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

public final class WinlatorInputProtocolTest {
    private static final String BUTTON_GOLDEN_VECTOR =
            "545049310100050004000000040302010807060504030201D0C0B0A002010700";

    public static void main(String[] args) {
        byte[] packet = new byte[
                ForwardedInputProtocol.HEADER_BYTES +
                ForwardedInputProtocol.MAXIMUM_PAYLOAD_BYTES];
        int length = ForwardedInputProtocol.writeButtonFrame(
                packet, 0x01020304L, 0x0102030405060708L, 0xA0B0C0D0L,
                2, 7, true);
        equal(BUTTON_GOLDEN_VECTOR, toHex(packet, length), "Java button golden vector");

        ForwardedInputProtocol.Header header =
                ForwardedInputProtocol.readHeader(packet, length);
        require(header != null, "Java header decode");
        equal(ForwardedInputProtocol.TYPE_BUTTON, header.type, "Java type");
        equal(4, header.payloadLength, "Java payload length");
        equal(0x01020304L, header.sequence, "Java sequence");
        equal(0x0102030405060708L, header.eventTimeNanoseconds, "Java event time");
        equal(0xA0B0C0D0L, header.deviceStableId, "Java device id");

        length = ForwardedInputProtocol.writeAxisFrame(
                packet, 5, 6, 7, 1, 15, Short.MIN_VALUE, 1234);
        require(ForwardedInputProtocol.readHeader(packet, length) != null, "Java axis header");

        length = ForwardedInputProtocol.writePointerAbsoluteFrame(
                packet, 6, 7, 8, 3, 2, 0, 0xffff, 32768,
                0xDEADBEEFL, 0x01020304L);
        require(ForwardedInputProtocol.readHeader(packet, length) != null, "Java pointer header");

        packet[0] = 'X';
        require(ForwardedInputProtocol.readHeader(packet, length) == null, "Java bad magic rejection");
        testMapping();
        testQueue();
        testProductionClient();
        testClientReconnect();
        System.out.println("TPI1 Java/.NET golden vector: PASS");
        System.out.println("TPI1 Java strict header validation: PASS");
        System.out.println("TPI1 Android control mapping/math: PASS");
        System.out.println("TPI1 Java preallocated SPSC queue: PASS");
        System.out.println("TPI1 production client authentication/sequencing: PASS");
        System.out.println("TPI1 production client overflow/reconnect reset: PASS");
    }

    private static void testMapping() {
        equal(ForwardedInputProtocol.BUTTON_UP,
                ForwardedInputMapping.mapKeyCode(19), "D-pad up mapping");
        equal(ForwardedInputProtocol.BUTTON_START,
                ForwardedInputMapping.mapKeyCode(108), "controller start mapping");
        equal(ForwardedInputProtocol.BUTTON_COIN,
                ForwardedInputMapping.mapKeyCode(109), "controller select/coin mapping");
        equal(ForwardedInputProtocol.BUTTON_TEST,
                ForwardedInputMapping.mapKeyCode(131), "keyboard test mapping");
        equal(ForwardedInputProtocol.BUTTON_SERVICE,
                ForwardedInputMapping.mapKeyCode(132), "keyboard service mapping");
        equal(ForwardedInputProtocol.BUTTON_6,
                ForwardedInputMapping.mapKeyCode(103), "controller R1 mapping");
        equal(ForwardedInputMapping.UNMAPPED,
                ForwardedInputMapping.mapKeyCode(104), "trigger remains analog by default");
        equal(ForwardedInputProtocol.BUTTON_7,
                ForwardedInputMapping.mapKeyCode(104, true), "APM3 L2 extension mapping");
        equal(ForwardedInputProtocol.BUTTON_8,
                ForwardedInputMapping.mapKeyCode(105, true), "APM3 R2 extension mapping");
        equal(ForwardedInputMapping.UNMAPPED,
                ForwardedInputMapping.mapKeyCode(4), "unmapped Android back key");
        equal((short)Short.MAX_VALUE, ForwardedInputMapping.toQ15(2.0f), "Q15 upper clamp");
        equal((short)-Short.MAX_VALUE, ForwardedInputMapping.toQ15(-2.0f), "Q15 lower clamp");
        equal(16384, ForwardedInputMapping.toUnsignedQ15(0.5f), "unsigned Q15 midpoint");
        equal(65535, ForwardedInputMapping.toUnsignedQ16(99.0f, 100), "Q16 extent maximum");

        ForwardedInputQueue full = new ForwardedInputQueue(2);
        byte[] first = full.tryAcquireWriteBuffer();
        require(first != null, "first bounded queue slot");
        full.publishWrite(ForwardedInputProtocol.writeFocusFrame(first, 1, 1, 0, true));
        byte[] second = full.tryAcquireWriteBuffer();
        require(second != null, "second bounded queue slot");
        full.publishWrite(ForwardedInputProtocol.writeFocusFrame(second, 2, 2, 0, false));
        require(full.tryAcquireWriteBuffer() == null, "bounded queue full signal");
    }

    private static void testProductionClient() {
        final String sessionId = "00112233445566778899aabbccddeeff";
        final byte[] token = new byte[32];
        for (int index = 0; index < token.length; index++) token[index] = (byte)(index + 1);
        AtomicReference<Throwable> serverFailure = new AtomicReference<>();

        try (ServerSocket server = new ServerSocket(0, 1, InetAddress.getByName("127.0.0.1"))) {
            Thread serverThread = new Thread(() -> {
                try (Socket socket = server.accept()) {
                    authenticate(socket, sessionId, token);
                    ForwardedInputProtocol.Header reset = readFrame(socket);
                    equal(0L, reset.deviceStableId, "reset device");
                    equal(1L, reset.sequence, "reset sequence");
                    ForwardedInputProtocol.Header focused = readFrame(socket);
                    equal(0L, focused.deviceStableId, "focused device");
                    equal(2L, focused.sequence, "focused sequence");

                    ForwardedInputProtocol.Header first = readFrame(socket);
                    ForwardedInputProtocol.Header second = readFrame(socket);
                    ForwardedInputProtocol.Header third = readFrame(socket);
                    equal(0x11111111L, first.deviceStableId, "first device id");
                    equal(1L, first.sequence, "first device sequence one");
                    equal(0x22222222L, second.deviceStableId, "second device id");
                    equal(1L, second.sequence, "second device sequence one");
                    equal(0x11111111L, third.deviceStableId, "first device id repeat");
                    equal(2L, third.sequence, "first device sequence two");
                }
                catch (Throwable error) {
                    serverFailure.set(error);
                }
            }, "tpi1-client-test-server");
            serverThread.start();

            try (ForwardedInputClient client = new ForwardedInputClient(
                    sessionId, server.getLocalPort(), token)) {
                client.start();
                require(client.awaitConnected(5000), "production client connect");
                require(client.sendButton(0x11111111L, 0,
                        ForwardedInputProtocol.BUTTON_COIN, true, 1), "first client frame");
                require(client.sendButton(0x22222222L, 0,
                        ForwardedInputProtocol.BUTTON_1, true, 2), "second client frame");
                require(client.sendButton(0x11111111L, 0,
                        ForwardedInputProtocol.BUTTON_COIN, false, 3), "third client frame");
                require(client.awaitDataFramesSent(3, 5000), "production client drain");
                equal(1L, client.getResynchronizations(), "initial client resynchronization");
                equal(0L, client.getDroppedFrames(), "initial client drops");
            }

            serverThread.join(5000);
            require(!serverThread.isAlive(), "production client server completion");
            if (serverFailure.get() != null)
                throw new IllegalStateException("Production client server failed", serverFailure.get());
        }
        catch (IOException | InterruptedException error) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Production client test failed", error);
        }

        try (ForwardedInputClient full = new ForwardedInputClient(sessionId, 1, token)) {
            for (int index = 0; index < 256; index++)
                require(full.sendFocus((index & 1) == 0, index + 1), "client queue capacity");
            require(!full.sendFocus(false, 300), "client queue overflow signal");
            equal(1L, full.getDroppedFrames(), "client dropped-frame counter");
        }
    }

    private static void testClientReconnect() {
        final String sessionId = "ffeeddccbbaa99887766554433221100";
        final byte[] token = new byte[32];
        Arrays.fill(token, (byte)0xA5);
        AtomicReference<Throwable> serverFailure = new AtomicReference<>();
        CountDownLatch firstClosed = new CountDownLatch(1);
        CountDownLatch secondReceived = new CountDownLatch(1);

        try (ServerSocket server = new ServerSocket(0, 2, InetAddress.getByName("127.0.0.1"))) {
            Thread serverThread = new Thread(() -> {
                try {
                    try (Socket first = server.accept()) {
                        authenticate(first, sessionId, token);
                        readFrame(first);
                        readFrame(first);
                        first.setSoLinger(true, 0);
                    }
                    firstClosed.countDown();

                    try (Socket second = server.accept()) {
                        authenticate(second, sessionId, token);
                        readFrame(second);
                        readFrame(second);
                        ForwardedInputProtocol.Header event = readFrame(second);
                        equal(0x33333333L, event.deviceStableId, "reconnected device id");
                        secondReceived.countDown();
                    }
                }
                catch (Throwable error) {
                    serverFailure.set(error);
                    firstClosed.countDown();
                    secondReceived.countDown();
                }
            }, "tpi1-reconnect-test-server");
            serverThread.start();

            try (ForwardedInputClient client = new ForwardedInputClient(
                    sessionId, server.getLocalPort(), token)) {
                client.start();
                require(client.awaitConnected(5000), "reconnect initial connection");
                require(firstClosed.await(5000, TimeUnit.MILLISECONDS), "server first close");
                for (int attempt = 0; attempt < 40 && secondReceived.getCount() != 0; attempt++) {
                    client.sendButton(0x33333333L, 0,
                            ForwardedInputProtocol.BUTTON_START,
                            (attempt & 1) == 0,
                            attempt + 1);
                    Thread.sleep(50);
                }
                require(secondReceived.await(5000, TimeUnit.MILLISECONDS),
                        "reconnected frame receipt");
                require(client.getResynchronizations() >= 2,
                        "reconnect focus reset count");
            }

            serverThread.join(5000);
            require(!serverThread.isAlive(), "reconnect server completion");
            if (serverFailure.get() != null)
                throw new IllegalStateException("Reconnect server failed", serverFailure.get());
        }
        catch (IOException | InterruptedException error) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Production client reconnect test failed", error);
        }
    }

    private static void authenticate(Socket socket, String sessionId, byte[] token)
            throws IOException {
        socket.setSoTimeout(5000);
        DataInputStream input = new DataInputStream(socket.getInputStream());
        DataOutputStream output = new DataOutputStream(socket.getOutputStream());
        byte[] fixed = new byte[58];
        input.readFully(fixed);
        require(fixed[0] == 'T' && fixed[1] == 'P' && fixed[2] == 'B' && fixed[3] == '1',
                "client TPB1 magic");
        require((fixed[4] & 0xff) == 0 && (fixed[5] & 0xff) == 1,
                "client TPB1 version");
        require((fixed[6] & 0xff) == 0 && (fixed[7] & 0xff) == 2,
                "client TPB1 input channel kind");
        equal(sessionId.toUpperCase(), toHex(Arrays.copyOfRange(fixed, 8, 24), 16),
                "client TPB1 session");
        require(Arrays.equals(token, Arrays.copyOfRange(fixed, 24, 56)),
                "client TPB1 token");
        int nameLength = ((fixed[56] & 0xff) << 8) | (fixed[57] & 0xff);
        byte[] name = new byte[nameLength];
        input.readFully(name);
        equal("TeknoParrot_ForwardedInput", new String(name, StandardCharsets.UTF_8),
                "client TPB1 channel name");
        output.write("OKAY".getBytes(StandardCharsets.US_ASCII));
        output.flush();
    }

    private static ForwardedInputProtocol.Header readFrame(Socket socket) throws IOException {
        DataInputStream input = new DataInputStream(socket.getInputStream());
        byte[] header = new byte[ForwardedInputProtocol.HEADER_BYTES];
        input.readFully(header);
        int payloadLength = (header[8] & 0xff) |
                ((header[9] & 0xff) << 8) |
                ((header[10] & 0xff) << 16) |
                ((header[11] & 0xff) << 24);
        byte[] packet = Arrays.copyOf(header, header.length + payloadLength);
        input.readFully(packet, header.length, payloadLength);
        ForwardedInputProtocol.Header decoded =
                ForwardedInputProtocol.readHeader(packet, packet.length);
        require(decoded != null, "production client TPI1 frame");
        return decoded;
    }

    private static void testQueue() {
        final int frameCount = 100000;
        ForwardedInputQueue queue = new ForwardedInputQueue(64);
        Thread consumer = new Thread(() -> {
            for (int expected = 0; expected < frameCount; expected++) {
                byte[] frame;
                while ((frame = queue.tryAcquireReadBuffer()) == null)
                    Thread.yield();
                int length = queue.acquiredReadLength();
                ForwardedInputProtocol.Header header =
                        ForwardedInputProtocol.readHeader(frame, length);
                require(header != null, "queued header");
                equal(Integer.toUnsignedLong(expected), header.sequence, "queued sequence");
                queue.releaseRead();
            }
        }, "tpi1-test-consumer");
        consumer.start();

        for (int sequence = 0; sequence < frameCount; sequence++) {
            byte[] frame;
            while ((frame = queue.tryAcquireWriteBuffer()) == null)
                Thread.yield();
            int length = ForwardedInputProtocol.writeButtonFrame(
                    frame, Integer.toUnsignedLong(sequence), sequence, 123, 0, 8,
                    (sequence & 1) != 0);
            queue.publishWrite(length);
        }
        try {
            consumer.join(30000);
        }
        catch (InterruptedException error) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("TPI1 queue test was interrupted", error);
        }
        require(!consumer.isAlive(), "queue consumer completion");
        equal(0, queue.size(), "queue final size");
    }

    private static String toHex(byte[] data, int length) {
        StringBuilder result = new StringBuilder(length * 2);
        for (int index = 0; index < length; index++)
            result.append(String.format("%02X", data[index] & 0xff));
        return result.toString();
    }

    private static void require(boolean condition, String name) {
        if (!condition)
            throw new IllegalStateException(name + " did not pass");
    }

    private static void equal(Object expected, Object actual, String name) {
        if (!expected.equals(actual))
            throw new IllegalStateException(
                    name + " mismatch: expected " + expected + ", got " + actual);
    }
}
