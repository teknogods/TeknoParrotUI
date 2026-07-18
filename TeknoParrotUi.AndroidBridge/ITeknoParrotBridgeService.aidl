package com.teknoparrot.bridge.v1;

/**
 * Emulator-first bridge contract shared by TeknoParrotUI and the probe APK.
 * Keep additions backward compatible: Winlator will consume this contract.
 */
interface ITeknoParrotBridgeService {
    int getProtocolVersion();
    String prepareTestSession(String clientName);
    String getSessionStatus(String sessionId);
    void stopTestSession(String sessionId);
}
