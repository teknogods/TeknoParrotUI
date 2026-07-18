package com.teknoparrot.bridge.v1;

/**
 * Production-direction transport fixture. TeknoParrotUI is the client and the
 * Winlator companion owns this service and the descriptor-backed page.
 */
interface ITeknoParrotWinlatorService {
    int getProtocolVersion();
    byte[] getCapabilities(int clientProtocolVersion);
    byte[] prepareSession(in byte[] spec);
    String launchPreparedGuestDiagnostic(String sessionId);
    String prepareTestSession(String clientName);
    String getSessionStatus(String sessionId);
    String runPipeProbe(String sessionId, int port, String tokenHex);
    String launchGuestBridgeDiagnostic(String sessionId, int containerId, int port);
    String getGuestBridgeDiagnosticStatus(String sessionId);
    void stopGuestBridgeDiagnostic(String sessionId);
    void stopTestSession(String sessionId);
    String runPreparedInputDiagnostic(String sessionId);
    String launchPreparedInputActivityDiagnostic(String sessionId);
    String launchPreparedActivity(in byte[] request);
    String ensureTeknoParrotEnvironment(int preferredContainerId);
}
