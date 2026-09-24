using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
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

            // Cxbx driving layout used by Crazy Taxi High Roller:
            // Analog2=wheel, Analog0=gas, Analog6=brake.
            InputCode.AnalogBytes[2] = 0x80;
            InputCode.AnalogBytes[0] = 0x00;
            InputCode.AnalogBytes[6] = 0x00;
            var cxbxEngine = new KeyboardAxisEngine();
            cxbxEngine.Initialize(new GameProfile
            {
                EmulationProfile = EmulationProfile.cxbxr,
                ConfigValues = new System.Collections.Generic.List<FieldInformation>
                {
                    new FieldInformation { FieldName = "Use Keyboard/Button For Axis", FieldValue = "1" }
                }
            });
            var wheelRight = new JoystickButtons { ButtonName = "Wheel Axis Right", AnalogType = AnalogType.Wheel };
            Check(cxbxEngine.HandleButton(wheelRight, true), "Cxbx wheel row consumed", ref failures);
            Check(cxbxEngine.HandleButton(gas, true), "Cxbx gas row consumed", ref failures);
            Check(cxbxEngine.HandleButton(brake, true), "Cxbx brake row consumed", ref failures);
            for (int i = 0; i < 5; i++)
                cxbxEngine.Tick();
            Check(InputCode.AnalogBytes[2] > 0x80, $"Cxbx wheel byte 2 ramped right (0x{InputCode.AnalogBytes[2]:X2})", ref failures);
            Check(InputCode.AnalogBytes[0] > 0x00, $"Cxbx gas byte 0 ramped up (0x{InputCode.AnalogBytes[0]:X2})", ref failures);
            Check(InputCode.AnalogBytes[6] > 0x00, $"Cxbx brake byte 6 ramped up (0x{InputCode.AnalogBytes[6]:X2})", ref failures);

            // Road Fighters 3D / Thrill Drive 4 now use the dedicated
            // KonamiAcioRacing layout: Analog0=wheel, Analog2=gas, Analog4=brake.
            InputCode.AnalogBytes[0] = 0x80;
            InputCode.AnalogBytes[2] = 0x00;
            InputCode.AnalogBytes[4] = 0x00;
            var konamiRacingEngine = new KeyboardAxisEngine();
            konamiRacingEngine.Initialize(new GameProfile
            {
                EmulationProfile = EmulationProfile.KonamiAcioRacing,
                ConfigValues = new System.Collections.Generic.List<FieldInformation>
                {
                    new FieldInformation { FieldName = "Use Keyboard/Button For Axis", FieldValue = "1" }
                }
            });
            Check(konamiRacingEngine.HandleButton(wheelRight, true), "Konami racing wheel row consumed", ref failures);
            Check(konamiRacingEngine.HandleButton(gas, true), "Konami racing gas row consumed", ref failures);
            Check(konamiRacingEngine.HandleButton(brake, true), "Konami racing brake row consumed", ref failures);
            for (int i = 0; i < 5; i++)
                konamiRacingEngine.Tick();
            Check(InputCode.AnalogBytes[0] > 0x80, $"Konami racing wheel byte 0 ramped right (0x{InputCode.AnalogBytes[0]:X2})", ref failures);
            Check(InputCode.AnalogBytes[2] > 0x00, $"Konami racing gas byte 2 ramped up (0x{InputCode.AnalogBytes[2]:X2})", ref failures);
            Check(InputCode.AnalogBytes[4] > 0x00, $"Konami racing brake byte 4 ramped up (0x{InputCode.AnalogBytes[4]:X2})", ref failures);

            // A newly imported System 22 flight profile has four independently
            // mapped keyboard axes. The old 2.0 engine only recognized 4/6.
            var flight = LoadStockProfile("airco22b.xml");
            flight.ConfigValues.First(f => f.FieldName == "Use Keyboard/Button For Axis").FieldValue = "1";
            var flightEngine = new KeyboardAxisEngine();
            flightEngine.Initialize(flight);
            var stickLeft = flight.JoystickButtons.First(b => b.ButtonName == "Analog X Left");
            var rudderRight = flight.JoystickButtons.First(b => b.ButtonName == "Analog Z Right");
            Check(flightEngine.HandleButton(stickLeft, true), "System 22 stick row consumed", ref failures);
            Check(flightEngine.HandleButton(rudderRight, true), "System 22 rudder row consumed", ref failures);
            flightEngine.Tick();
            Check(InputCode.AnalogBytes[0] < 128, "System 22 stick ramps left on profile axis 0", ref failures);
            Check(InputCode.AnalogBytes[4] > 128, "System 22 rudder ramps right on profile axis 4", ref failures);

            // System 21 profiles use Minimum/Maximum button rows and a
            // profile-selected step while the analog stick row is hidden.
            var s21 = LoadStockProfile("aircomb.xml");
            s21.ConfigValues.First(f => f.FieldName == "Use Keyboard/Button For Axis").FieldValue = "1";
            var s21Engine = new KeyboardAxisEngine();
            s21Engine.Initialize(s21);
            var s21Left = s21.JoystickButtons.First(b => b.ButtonName == "Stick X Left");
            Check(s21Engine.HandleButton(s21Left, true), "System 21 direction row consumed", ref failures);
            s21Engine.Tick();
            Check(InputCode.AnalogBytes[0] < 128, "System 21 stick ramps left", ref failures);
            s21Engine.HandleButton(s21Left, false);
            for (int i = 0; i < 30; i++) s21Engine.Tick();
            Check(InputCode.AnalogBytes[0] == 128, "System 21 stick returns to center", ref failures);

            Console.WriteLine(failures == 0
                ? "\nKeyboard-axis engine (legacy + System 21/22 layouts): ALL CHECKS PASSED"
                : $"\nKeyboard-axis engine: {failures} FAILURE(S)");
            return failures == 0 ? 0 : 1;
        }

        private static void Check(bool ok, string what, ref int failures)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
            if (!ok)
                failures++;
        }

        private static GameProfile LoadStockProfile(string fileName)
        {
            using var stream = File.OpenRead(Path.Combine("TeknoParrotUi.Common", "GameProfiles", fileName));
            return (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
        }
    }
}
