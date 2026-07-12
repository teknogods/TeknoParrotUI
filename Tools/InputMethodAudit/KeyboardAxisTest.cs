using System;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;

namespace InputMethodAudit
{
    /// <summary>
    /// Regression check for keyboard-driven wheel/gas/brake on Linux (Sega
    /// Rally 3 report: "keyboard does nothing in game"). Simulates exactly
    /// what EvdevMouseListener feeds the shared KeyboardAxisEngine and
    /// verifies the analog bytes ramp.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- kbd-axis-test
    /// </summary>
    internal static class KeyboardAxisTest
    {
        public static int Run()
        {
            int failures = 0;

            var profile = new GameProfile
            {
                EmulationProfile = EmulationProfile.EuropaRSegaRally3,
                ConfigValues = new System.Collections.Generic.List<FieldInformation>
                {
                    new FieldInformation { FieldName = "Use Keyboard/Button For Axis", FieldValue = "1" },
                    new FieldInformation { FieldName = "Keyboard/Button Axis X/Y Sensitivity", FieldValue = "15" }
                }
            };

            var engine = new KeyboardAxisEngine();
            engine.Initialize(profile);
            Check(engine.Enabled, "engine enabled via 'Use Keyboard/Button For Axis'", ref failures);

            var wheelLeft = new JoystickButtons { ButtonName = "Wheel Axis Left", AnalogType = AnalogType.Wheel };
            var gas = new JoystickButtons { ButtonName = "Gas Axis", AnalogType = AnalogType.Gas };
            var brake = new JoystickButtons { ButtonName = "Brake Axis", AnalogType = AnalogType.Brake };

            // Rows must be consumed by the engine (not fall through to digital dispatch)
            InputCode.AnalogBytes[0] = 0x80;
            InputCode.AnalogBytes[2] = 0x00;
            InputCode.AnalogBytes[4] = 0x00;
            Check(engine.HandleButton(wheelLeft, true), "wheel row consumed by axis engine", ref failures);
            Check(engine.HandleButton(gas, true), "gas row consumed by axis engine", ref failures);
            Check(engine.HandleButton(brake, true), "brake row consumed by axis engine", ref failures);

            // Held for 5 ticks: wheel ramps down from center, pedals ramp up
            for (int i = 0; i < 5; i++)
                engine.Tick();
            Check(InputCode.AnalogBytes[0] < 0x80, $"wheel byte ramped left (0x{InputCode.AnalogBytes[0]:X2} < 0x80)", ref failures);
            Check(InputCode.AnalogBytes[2] > 0x00, $"gas byte ramped up (0x{InputCode.AnalogBytes[2]:X2})", ref failures);
            Check(InputCode.AnalogBytes[4] > 0x00, $"brake byte ramped up (0x{InputCode.AnalogBytes[4]:X2})", ref failures);

            // Release: wheel returns to center, pedals to rest
            byte wheelHeld = InputCode.AnalogBytes[0];
            engine.HandleButton(wheelLeft, false);
            engine.HandleButton(gas, false);
            engine.HandleButton(brake, false);
            for (int i = 0; i < 30; i++)
                engine.Tick();
            Check(InputCode.AnalogBytes[0] == 0x80, $"wheel returned to center (0x{InputCode.AnalogBytes[0]:X2})", ref failures);
            Check(InputCode.AnalogBytes[0] > wheelHeld, "wheel moved back from held position", ref failures);
            Check(InputCode.AnalogBytes[2] == 0x00, $"gas returned to rest (0x{InputCode.AnalogBytes[2]:X2})", ref failures);

            // Digital rows must NOT be consumed (Start etc. stay on MappingDispatch)
            var start = new JoystickButtons { ButtonName = "Start", AnalogType = AnalogType.None, InputMapping = InputMapping.P1ButtonStart };
            Check(!engine.HandleButton(start, true), "digital row not consumed by axis engine", ref failures);

            // Engine off => rows fall through (matches Windows behaviour)
            var offEngine = new KeyboardAxisEngine();
            offEngine.Initialize(new GameProfile
            {
                EmulationProfile = EmulationProfile.EuropaRSegaRally3,
                ConfigValues = new System.Collections.Generic.List<FieldInformation>()
            });
            Check(!offEngine.Enabled, "engine disabled without the config flag", ref failures);
            Check(!offEngine.HandleButton(wheelLeft, true), "row not consumed when engine off", ref failures);

            Console.WriteLine(failures == 0
                ? "\nKeyboard-axis engine (Sega Rally 3 layout): ALL CHECKS PASSED"
                : $"\nKeyboard-axis engine: {failures} FAILURE(S)");
            return failures == 0 ? 0 : 1;
        }

        private static void Check(bool ok, string what, ref int failures)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
            if (!ok)
                failures++;
        }
    }
}
