using System;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

internal static class GameErrorFormatter
{
    public static string Format(EmulatorType emulator, int exitCode, string? diagnostics)
    {
        if (emulator is EmulatorType.TeknoViper or EmulatorType.TeknoVegas)
        {
            var key = exitCode switch
            {
                0x05650001 => "GameErrorViperVegasInvalidConfiguration",
                0x05650002 => "GameErrorViperVegasUnsupportedGame",
                0x05650003 => "GameErrorViperVegasLicense",
                0x05650004 => "GameErrorViperVegasMedia",
                0x05650005 => "GameErrorViperVegasState",
                0x05650006 => "GameErrorViperVegasNetwork",
                0x05650007 => "GameErrorViperVegasHostInitialization",
                _ => "GameErrorViperVegasUnexpected"
            };
            return WithDiagnostics(Loc.T(key, "The emulator could not start or exited unexpectedly."),
                exitCode, diagnostics);
        }

        if (emulator == EmulatorType.TeknoS21)
        {
            var summary = "TeknoS21 could not start or exited unexpectedly.";
            var failure = diagnostics?.LastIndexOf("TeknoS21 failed while ", StringComparison.Ordinal) ?? -1;
            var detail = failure >= 0 ? diagnostics![failure..] : diagnostics;
            if (string.IsNullOrWhiteSpace(detail))
                detail = unchecked((uint)exitCode) == 0xC0000005u
                    ? "The emulator encountered a memory access violation."
                    : "The emulator exited before it could log the cause.";
            return WithDiagnostics(summary, exitCode, detail);
        }

        var message = emulator switch
        {
            EmulatorType.TeknoTPJC or EmulatorType.TeknoS11 => $"{emulator} exited with an error.",
            EmulatorType.TeknoGClub => exitCode == 5
                ? "TeknoGClub could not authorize the stored TeknoParrot serial."
                : "TeknoGClub could not launch or continue. Check the selected ROM ZIP and emulator settings.",
            EmulatorType.TeknoS23 => exitCode switch
            {
                5 => "TeknoS23 could not authorize the stored TeknoParrot serial.",
                64 => "This game is available in TeknoS23 developer builds only.",
                _ => "TeknoS23 could not launch or continue. Check the selected ROM ZIP and emulator settings."
            },
            EmulatorType.TeknoS22 => exitCode switch
            {
                63 => "TeknoS22 could not authorize the stored TeknoParrot serial.",
                64 => "This game is available in TeknoS22 developer builds only.",
                _ => "TeknoS22 could not launch or continue. Check the selected ROM ZIP and emulator settings."
            },
            EmulatorType.TeknoAGX => exitCode == 12
                ? "TeknoAGX could not authorize the stored TeknoParrot serial. Activate your license through ElfLoader."
                : "TeknoAGX could not launch or continue. Check the selected ROM ZIP, CHD and emulator error message.",
            EmulatorType.TeknoM2 => exitCode switch
            {
                12 => "TeknoM2 could not authorize the stored TeknoParrot serial. Activate it through ElfLoader.",
                3 => "This game is disabled in this TeknoM2 build.",
                2 => "TeknoM2 received an invalid launch configuration.",
                _ => "TeknoM2 could not launch or continue. Check the selected ROM ZIP, CHD and emulator settings."
            },
            EmulatorType.TeknoVUnit or EmulatorType.TeknoHornet or EmulatorType.TeknoModel1 or
                EmulatorType.TeknoModel2 or EmulatorType.TeknoZeus or EmulatorType.TeknoHNG64 or
                EmulatorType.TeknoCobra => exitCode == 2
                    ? $"{emulator} received an invalid launch configuration."
                    : $"{emulator} could not start or exited unexpectedly.",
            _ => "The game exited with an error."
        };
        return WithDiagnostics(message, exitCode, diagnostics);
    }

    private static string WithDiagnostics(string summary, int exitCode, string? diagnostics) =>
        summary + Environment.NewLine + $"Exit code: 0x{exitCode:X8}" +
        (string.IsNullOrWhiteSpace(diagnostics) ? "" :
            Environment.NewLine + Environment.NewLine + diagnostics.Trim());
}
