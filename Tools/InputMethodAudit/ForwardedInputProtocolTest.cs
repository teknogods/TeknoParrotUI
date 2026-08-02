using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.InputListening.Forwarded;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.Jvs;

namespace InputMethodAudit
{
    internal static class ForwardedInputProtocolTest
    {
        private const string ButtonGoldenVector =
            "545049310100050004000000040302010807060504030201D0C0B0A002010700";

        public static int Run()
        {
            try
            {
                Span<byte> packet = stackalloc byte[
                    ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];

                var length = ForwardedInputProtocol.WriteButtonFrame(
                    packet, 0x01020304, 0x0102030405060708, 0xA0B0C0D0,
                    2, ForwardedInputButton.Coin, true);
                Equal(ButtonGoldenVector, Convert.ToHexString(packet[..length]), "button golden vector");
                True(ForwardedInputProtocol.TryReadButton(
                    packet[..length], out var header, out var player, out var button, out var pressed),
                    "button decode");
                Equal((uint)0x01020304, header.Sequence, "sequence");
                Equal((ulong)0x0102030405060708, header.EventTimeNanoseconds, "event time");
                Equal((uint)0xA0B0C0D0, header.DeviceStableId, "device id");
                Equal((byte)2, player, "button player");
                Equal(ForwardedInputButton.Coin, button, "button id");
                True(pressed, "button pressed");

                length = ForwardedInputProtocol.WriteAxisFrame(
                    packet, 5, 6, 7, 1, 15, short.MinValue, 1234);
                True(ForwardedInputProtocol.TryReadAxis(
                    packet[..length], out _, out player, out var axisId,
                    out var axisValue, out var flat), "axis decode");
                Equal((byte)1, player, "axis player");
                Equal((ushort)15, axisId, "axis id");
                Equal(short.MinValue, axisValue, "axis Q15");
                Equal((ushort)1234, flat, "axis flat");

                length = ForwardedInputProtocol.WritePointerAbsoluteFrame(
                    packet, 6, 7, 8, 3, 2, 0, ushort.MaxValue, 32768,
                    0xDEADBEEF, 0x01020304);
                True(ForwardedInputProtocol.TryReadPointerAbsolute(
                    packet[..length], out _, out player, out var pointer), "pointer decode");
                Equal((byte)3, player, "pointer player");
                Equal((uint)0xDEADBEEF, pointer.PointerId, "pointer id");
                Equal((ushort)0, pointer.X, "pointer x");
                Equal(ushort.MaxValue, pointer.Y, "pointer y");
                Equal((ushort)32768, pointer.Pressure, "pointer pressure");

                ValidateMalformedPackets(packet);
                ValidateStateBoundary(packet);
                ValidateLatchedTestSwitch(packet);
                ValidateArcadeAxisDigitalization(packet);
                ValidateJvsStreamDecoder();
                ValidateTaitoTypeXJvsControls();
                ValidateTaitoGunJvsControls();
                ValidateBattleGearKeyForwarding();
                ValidateVirtuaRLimitForwarding();
                ValidateWmmtForwarding();
                ValidateMkdxForwarding();
                ValidateInitialDForwarding();
                ValidateSegaRingDrivingForwarding();
                ValidateSegaRingGunForwarding();
                ValidateMachStormForwarding();
                ValidateDominantAxisCapture();
                ValidateSequenceWrap(packet);
                ValidateChunkedStream();
                ValidateCancelledAsyncStream();

                Console.WriteLine("TPI1 header/golden vector: PASS");
                Console.WriteLine("TPI1 strict payload validation: PASS");
                Console.WriteLine("TPI1 device state/gap/release behavior: PASS");
                Console.WriteLine("TPI1 cabinet TEST switch latching: PASS");
                Console.WriteLine("TPI1 gamepad stick/hat arcade directions: PASS");
                Console.WriteLine("JVS chunking/escaping/resynchronization: PASS");
                Console.WriteLine("Taito Type X forwarded controls/JVS reply: PASS");
                Console.WriteLine("Taito gun cabinet Start/Service/Coin mapping: PASS");
                Console.WriteLine("Valve Limit R forwarded controls/analogs: PASS");
                Console.WriteLine("WMMT forwarded controls/JVS analogs: PASS");
                Console.WriteLine("Mario Kart DX forwarded controls/JVS analogs: PASS");
                Console.WriteLine("Sega Ring driving controls/JVS analogs: PASS");
                Console.WriteLine("Sega Ring gun pointer/controller JVS analogs: PASS");
                Console.WriteLine("TPI1 uint32 sequence wrap: PASS");
                Console.WriteLine("TPI1 partial-read stream framing/EOF release: PASS");
                Console.WriteLine("TPI1 asynchronous read cancellation/release: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Forwarded input protocol test failed: " + error.Message);
                return 1;
            }
        }

        private static void ValidateMalformedPackets(Span<byte> packet)
        {
            var length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 2, 3, 0, ForwardedInputButton.Start, true);
            packet[0] = (byte)'X';
            False(ForwardedInputProtocol.TryReadHeader(packet[..length], out _), "bad magic");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 2, 3, 0, ForwardedInputButton.Start, true);
            packet[4] = 2;
            False(ForwardedInputProtocol.TryReadHeader(packet[..length], out _), "bad version");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 2, 3, 0, ForwardedInputButton.Start, true);
            packet[8] = 5;
            False(ForwardedInputProtocol.TryReadHeader(packet[..length], out _), "bad payload length");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 2, 3, 0, ForwardedInputButton.Start, true);
            packet[ForwardedInputProtocol.HeaderBytes + 1] = 2;
            False(ForwardedInputProtocol.TryReadButton(
                packet[..length], out _, out _, out _, out _), "non-boolean pressed value");
        }

        private static void ValidateStateBoundary(Span<byte> packet)
        {
            const uint deviceA = 100;
            const uint deviceB = 200;
            var source = new WinlatorForwardedInputSource();

            var length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 10, deviceA, 0, ForwardedInputButton.Button1, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "first edge");
            Equal(ForwardedInputApplyResult.StaleSequence, source.ApplyFrame(packet[..length]), "duplicate edge");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 3, 11, deviceA, 0, ForwardedInputButton.Button2, true);
            Equal(ForwardedInputApplyResult.SequenceGap, source.ApplyFrame(packet[..length]), "sequence gap");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 12, deviceB, 0, ForwardedInputButton.Button2, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "second device");
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 2, 13, deviceB, 0, ForwardedInputButton.Button7, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "APM3 extension edge");
            source.PublishDigitalButtonsToInputCode();
            False(InputCode.PlayerDigitalButtons[0].Button1 == true, "gap clears missed held edge");
            True(InputCode.PlayerDigitalButtons[0].Button2 == true, "aggregate held edge");
            True(InputCode.PlayerDigitalButtons[0].ExtensionButton1 == true,
                "extension button publication");

            length = ForwardedInputProtocol.WriteEmptyFrame(
                packet, ForwardedInputFrameType.DeviceRemoved, 3, 14, deviceB);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "device removal");
            source.PublishDigitalButtonsToInputCode();
            True(InputCode.PlayerDigitalButtons[0].Button2 == true,
                "other device still owns the aggregate edge");

            length = ForwardedInputProtocol.WriteFocusFrame(packet, 1, 14, 0, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "focus loss");
            source.PublishDigitalButtonsToInputCode();
            False(InputCode.PlayerDigitalButtons[0].Button2 == true, "focus loss releases held controls");

            length = ForwardedInputProtocol.WriteAxisFrame(packet, 4, 15, deviceA, 2, 3, 12345, 22);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "axis state");
            Span<uint> buttons = stackalloc uint[WinlatorForwardedInputSource.MaximumPlayers];
            Span<short> axes = stackalloc short[
                WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
            Span<ushort> flats = stackalloc ushort[
                WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
            Span<ForwardedPointerState> pointers = stackalloc ForwardedPointerState[
                WinlatorForwardedInputSource.MaximumPlayers];
            True(source.TryCopyDeviceState(deviceA, buttons, axes, flats, pointers), "copy state");
            Equal((short)12345, axes[2 * WinlatorForwardedInputSource.MaximumAxes + 3], "copied axis");
            Equal((ushort)22, flats[2 * WinlatorForwardedInputSource.MaximumAxes + 3], "copied flat");
        }

        private static void ValidateSequenceWrap(Span<byte> packet)
        {
            const uint device = 300;
            var source = new WinlatorForwardedInputSource();
            var length = ForwardedInputProtocol.WriteButtonFrame(
                packet, uint.MaxValue, 1, device, 0, ForwardedInputButton.Test, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "max sequence");
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 0, 2, device, 0, ForwardedInputButton.Test, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]), "wrapped sequence");
        }

        private static void ValidateLatchedTestSwitch(Span<byte> packet)
        {
            const uint device = 240;
            var source = new WinlatorForwardedInputSource(latchTestSwitch: true);
            Span<uint> buttons = stackalloc uint[WinlatorForwardedInputSource.MaximumPlayers];
            Span<short> axes = stackalloc short[
                WinlatorForwardedInputSource.MaximumPlayers *
                WinlatorForwardedInputSource.MaximumAxes];
            var testMask = 1u << (int)ForwardedInputButton.Test;

            var length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 1, device, 0, ForwardedInputButton.Test, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "latched TEST first press");
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 2, 2, device, 0, ForwardedInputButton.Test, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "latched TEST first release");
            source.CopyAggregateState(buttons, axes);
            True((buttons[0] & testMask) != 0,
                "latched TEST remains on after the touch button is released");

            length = ForwardedInputProtocol.WriteFocusFrame(packet, 3, 3, device, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "latched TEST focus release");
            source.CopyAggregateState(buttons, axes);
            True((buttons[0] & testMask) != 0,
                "cabinet TEST switch survives a transient Android focus loss");

            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 4, 4, device, 0, ForwardedInputButton.Test, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "latched TEST second press");
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 5, 5, device, 0, ForwardedInputButton.Test, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "latched TEST second release");
            source.CopyAggregateState(buttons, axes);
            True((buttons[0] & testMask) == 0,
                "second TEST press turns the cabinet switch off");

            var momentary = new WinlatorForwardedInputSource();
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 1, device, 0, ForwardedInputButton.Test, true);
            momentary.ApplyFrame(packet[..length]);
            length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 2, 2, device, 0, ForwardedInputButton.Test, false);
            momentary.ApplyFrame(packet[..length]);
            momentary.CopyAggregateState(buttons, axes);
            True((buttons[0] & testMask) == 0,
                "ordinary games retain momentary TEST behavior");
        }

        private static void ValidateArcadeAxisDigitalization(Span<byte> packet)
        {
            const uint gamepad = 250;
            var source = new WinlatorForwardedInputSource();

            var length = ForwardedInputProtocol.WriteAxisFrame(
                packet, 1, 1, gamepad, 2, 0, -20000, 1000);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "player 3 primary X axis");
            length = ForwardedInputProtocol.WriteAxisFrame(
                packet, 2, 2, gamepad, 2, 7, 20000, 1000);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "player 3 hat Y axis");

            source.PublishDigitalButtonsToInputCode();
            False(InputCode.PlayerDigitalButtons[2].Left == true,
                "standard publication leaves analog X analog");
            False(InputCode.PlayerDigitalButtons[2].Down == true,
                "standard publication leaves hat Y analog");

            source.PublishControlsToInputCode(digitalizeStickDirections: true);
            True(InputCode.PlayerDigitalButtons[2].Left == true,
                "arcade publication maps primary X to left");
            True(InputCode.PlayerDigitalButtons[2].Down == true,
                "arcade publication maps hat Y to down");
            False(InputCode.PlayerDigitalButtons[2].Right == true,
                "arcade publication does not invert primary X");
            False(InputCode.PlayerDigitalButtons[2].Up == true,
                "arcade publication does not invert hat Y");

            source.ReleaseAll();
            source.PublishControlsToInputCode(digitalizeStickDirections: true);
            False(InputCode.PlayerDigitalButtons[2].Left == true,
                "axis release clears left");
            False(InputCode.PlayerDigitalButtons[2].Down == true,
                "axis release clears down");
        }

        private static void ValidateJvsStreamDecoder()
        {
            var decoder = new JvsPacketDecoder();
            byte[] decoded = null;
            // Logical packet E0 01 04 D0 E0 00 00. D0 and the payload E0 are
            // byte-stuffed on the wire and delivered one byte at a time.
            var wire = new byte[] { 0x44, 0xE0, 0x01, 0x04, 0xD0, 0xCF, 0xD0, 0xDF, 0x00, 0x00 };
            foreach (var value in wire)
            {
                if (decoder.TryPush(value, out var packet))
                    decoded = packet;
            }
            Equal("E00104D0E00000", Convert.ToHexString(decoded ?? Array.Empty<byte>()),
                "escaped JVS packet");

            // An incomplete packet must be discarded when a fresh unescaped
            // sync byte arrives; the following complete reset packet wins.
            var resync = new byte[] { 0xE0, 0x01, 0x20, 0x11,
                                      0xE0, 0xFF, 0x03, 0xF0, 0xD9, 0xCB };
            decoded = null;
            foreach (var value in resync)
            {
                if (decoder.TryPush(value, out var packet))
                    decoded = packet;
            }
            Equal("E0FF03F0D9CB", Convert.ToHexString(decoded ?? Array.Empty<byte>()),
                "JVS stream resynchronization");

            decoder.Reset();
            False(decoder.TryPush(0xCF, out _), "orphan escape tail rejected");
        }

        private static void ValidateTaitoTypeXJvsControls()
        {
            var profile = new GameProfile
            {
                ProfileName = "3DCosplayMahjong",
                EmulationProfile = EmulationProfile.TaitoTypeXGeneric,
                ConfigValues = new List<FieldInformation>()
            };
            JvsPackageEmulator.Initialize(profile);
            Array.Clear(JvsPackageEmulator.Coins, 0, JvsPackageEmulator.Coins.Length);
            Array.Clear(JvsPackageEmulator.CoinStates, 0, JvsPackageEmulator.CoinStates.Length);
            JvsSetup.ConfigureJvsPackage(profile);
            Equal((byte)0x30, JvsPackageEmulator.JvsVersion, "Taito Type X JVS version");
            Equal((byte)0x18, JvsPackageEmulator.JvsSwitchCount, "Taito Type X switch count");
            True(JvsPackageEmulator.TaitoStick, "Taito Type X stick mode");

            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Start,
                         ForwardedInputButton.Up,
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9004, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Taito Type X forwarded " + button);
            }
            source.PublishControlsToJvsInputCode();

            var request = JvsHelper.CraftJvsPackage(0x01, new byte[] { 0x20, 0x02, 0x02 });
            Equal("E0010420020228", Convert.ToHexString(request), "Taito digital request");
            var reply = JvsPackageEmulator.GetReply(request);
            Equal("E00008010100A38000002D", Convert.ToHexString(reply),
                "Taito Start/directions/buttons JVS reply");

            var coinPressLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9004, 0, ForwardedInputButton.Coin, true);
            Equal(ForwardedInputApplyResult.Applied,
                source.ApplyFrame(frame[..coinPressLength]), "Taito coin press");
            source.PublishControlsToJvsInputCode();
            Equal(0, JvsPackageEmulator.Coins[0], "coin waits for release");

            var coinReleaseLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9004, 0, ForwardedInputButton.Coin, false);
            Equal(ForwardedInputApplyResult.Applied,
                source.ApplyFrame(frame[..coinReleaseLength]), "Taito coin release");
            source.PublishControlsToJvsInputCode();
            Equal(1, JvsPackageEmulator.Coins[0], "coin release increments counter");

            source.ReleaseAll();
            source.PublishControlsToJvsInputCode();
        }

        private static void ValidateTaitoGunJvsControls()
        {
            var profile = new GameProfile
            {
                ProfileName = "HauntedMuseum",
                EmulationProfile = EmulationProfile.HauntedMuseum,
                ConfigValues = new List<FieldInformation>()
            };
            JvsPackageEmulator.Initialize(profile);
            Array.Clear(JvsPackageEmulator.Coins, 0, JvsPackageEmulator.Coins.Length);
            Array.Clear(JvsPackageEmulator.CoinStates, 0, JvsPackageEmulator.CoinStates.Length);
            JvsSetup.ConfigureJvsPackage(profile);

            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Start,
                         ForwardedInputButton.Service,
                         ForwardedInputButton.Coin
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9025, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Taito gun forwarded " + button);
            }

            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGun);
            var player = InputCode.PlayerDigitalButtons[0];
            True(player.Start == false, "Taito gun ordinary JVS Start cleared");
            True(player.Up == true, "Taito gun P1 Start maps to P1 Up");
            True(player.Service == false && player.ExtensionButton4 == true,
                "Taito gun Service maps to extension switch 4");
            True(player.Coin == false && player.ExtensionButton1 == true,
                "Taito gun Coin maps to extension switch 1");
            Equal((byte)0x20, JvsPackageEmulator.GetPlayerControls(0),
                "Taito gun primary cabinet switch byte");
            Equal((byte)0x09, JvsPackageEmulator.GetPlayerControlsExt(0),
                "Taito gun extended cabinet switch byte");

            source.ReleaseAll();
            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGun);

            var hauntedMuseum2Source = new WinlatorForwardedInputSource();
            var actionLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9060, 0,
                ForwardedInputButton.Button3, true);
            Equal(ForwardedInputApplyResult.Applied,
                hauntedMuseum2Source.ApplyFrame(frame[..actionLength]),
                "Haunted Museum II forwarded Action");
            hauntedMuseum2Source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2);
            var hauntedMuseum2Player = InputCode.PlayerDigitalButtons[0];
            True(hauntedMuseum2Player.ExtensionButton1_8 == true,
                "Haunted Museum II Action maps to extension switch 18");
            True(hauntedMuseum2Player.Button3 == false,
                "Haunted Museum II Action clears ordinary JVS Button3");
            hauntedMuseum2Source.ReleaseAll();
            hauntedMuseum2Source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2);

            var eadpSource = new WinlatorForwardedInputSource();
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Start,
                         ForwardedInputButton.Button2
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9071, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied,
                    eadpSource.ApplyFrame(frame[..length]),
                    "EADP forwarded " + button);
            }
            eadpSource.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedEadp);
            var eadpPlayer = InputCode.PlayerDigitalButtons[0];
            True(eadpPlayer.Start == false &&
                 eadpPlayer.ExtensionButton3 == true,
                "EADP Enter maps from the overlay Start position to extension switch 3");
            True(eadpPlayer.Button2 == false &&
                 eadpPlayer.ExtensionButton4 == true,
                "EADP Select maps to extension switch 4");
            eadpSource.ReleaseAll();
            eadpSource.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedEadp);

            var musicSource = new WinlatorForwardedInputSource();
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Start,
                         ForwardedInputButton.Service,
                         ForwardedInputButton.Coin,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button4
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9074, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied,
                    musicSource.ApplyFrame(frame[..length]),
                    "Music Gun Gun 2 forwarded " + button);
            }
            musicSource.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic);
            var musicPlayer = InputCode.PlayerDigitalButtons[0];
            True(musicPlayer.Start == true,
                "Music Gun Gun 2 preserves ordinary Decision/Start");
            True(musicPlayer.Service == false &&
                 musicPlayer.ExtensionButton4 == true,
                "Music Gun Gun 2 Service maps to extension switch 4");
            True(musicPlayer.Coin == false &&
                 musicPlayer.ExtensionButton1 == true,
                "Music Gun Gun 2 Coin maps to extension switch 1");
            True(musicPlayer.Button2 == false &&
                 musicPlayer.ExtensionButton3 == true,
                "Music Gun Gun 2 Select maps to extension switch 3");
            True(musicPlayer.Button4 == false &&
                 musicPlayer.ExtensionButton2 == true,
                "Music Gun Gun 2 Enter maps to extension switch 2");
            musicSource.ReleaseAll();
            musicSource.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic);
        }

        private static void ValidateVirtuaRLimitForwarding()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3,
                         ForwardedInputButton.Button4,
                         ForwardedInputButton.Button5
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9006, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Valve Limit R forwarded " + button);
            }

            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: (short)0),
                         (Axis: (ushort)5, Value: short.MaxValue),
                         (Axis: (ushort)4, Value: (short)16384)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9006, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Valve Limit R forwarded axis " + axis);
            }

            byte sharedWheel = 0;
            JvsHelper.ConfigureExternalState(
                (offset, value) =>
                {
                    if (offset == 4)
                        sharedWheel = value;
                },
                () => sharedWheel = 0);
            try
            {
                source.PublishControlsToJvsInputCode(
                    AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit);
                var player = InputCode.PlayerDigitalButtons[0];
                True(player.Up == true, "Valve Limit R nitro mapping");
                True(player.Down == true, "Valve Limit R view mapping");
                True(player.Left == true, "Valve Limit R side-brake mapping");
                True(player.Right == true, "Valve Limit R shift-up mapping");
                True(player.Button1 == true, "Valve Limit R shift-down mapping");
                Equal((byte)127, InputCode.AnalogBytes[20], "Valve Limit R wheel channel");
                Equal((byte)127, sharedWheel, "Valve Limit R shared wheel byte");
                Equal(byte.MaxValue, InputCode.AnalogBytes[2], "Valve Limit R gas channel");
                Equal((byte)127, InputCode.AnalogBytes[4], "Valve Limit R brake channel");
            }
            finally
            {
                JvsHelper.ConfigureExternalState(null, null);
                source.ReleaseAll();
                source.PublishControlsToJvsInputCode(
                    AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit);
            }
        }

        private static void ValidateBattleGearKeyForwarding()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;

            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsBattleGear);
            True(InputCode.PlayerDigitalButtons[0].Right == true,
                "Battle Gear starts with the active-low entry key sensor off");

            var pressLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9008, 0, ForwardedInputButton.Right, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..pressLength]),
                "Battle Gear key press");
            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsBattleGear);
            True(InputCode.PlayerDigitalButtons[0].Right == false,
                "Battle Gear key inserts on first press using the active-low sensor");

            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsBattleGear);
            True(InputCode.PlayerDigitalButtons[0].Right == false,
                "Battle Gear key remains inserted while held");

            var releaseLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9008, 0, ForwardedInputButton.Right, false);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..releaseLength]),
                "Battle Gear key release");
            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsBattleGear);
            True(InputCode.PlayerDigitalButtons[0].Right == false,
                "Battle Gear key remains inserted after release");

            pressLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9008, 0, ForwardedInputButton.Right, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..pressLength]),
                "Battle Gear second key press");
            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsBattleGear);
            True(InputCode.PlayerDigitalButtons[0].Right == true,
                "Battle Gear key ejects on second press using the active-low sensor");
        }

        private static void ValidateWmmtForwarding()
        {
            var profile = new GameProfile
            {
                ProfileName = "WMMT6R",
                EmulationProfile = EmulationProfile.NamcoWmmt5,
                ConfigValues = new List<FieldInformation>()
            };
            JvsPackageEmulator.Initialize(profile);
            JvsSetup.ConfigureJvsPackage(profile);

            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: short.MaxValue),
                         (Axis: (ushort)5, Value: (short)16384),
                         (Axis: (ushort)4, Value: (short)8192)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9012, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "WMMT forwarded axis " + axis);
            }

            foreach (var button in new[]
                     {
                         ForwardedInputButton.Up,
                         ForwardedInputButton.Down,
                         ForwardedInputButton.Button5
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9012, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "WMMT forwarded " + button);
            }

            try
            {
                source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsWmmt);
                var player = InputCode.PlayerDigitalButtons[0];
                True(player.Up == true, "WMMT test-menu up mapping");
                True(player.Down == true, "WMMT test-menu down mapping");
                True(player.Button1 == true, "WMMT test-menu enter mapping");
                True(player.Button2 == false, "WMMT unused button 2 cleared");
                True(player.Button3 == true && player.Button4 == false &&
                     player.Button5 == true && player.Button6 == false,
                    "WMMT first-gear sensor encoding");
                Equal(byte.MaxValue, InputCode.AnalogBytes[0], "WMMT wheel channel");
                Equal((byte)127, InputCode.AnalogBytes[2], "WMMT gas channel");
                Equal((byte)63, InputCode.AnalogBytes[4], "WMMT brake channel");

                var shiftLength = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9012, 0,
                    ForwardedInputButton.Button2, true);
                Equal(ForwardedInputApplyResult.Applied,
                    source.ApplyFrame(frame[..shiftLength]), "WMMT shift-up press");
                source.PublishControlsToJvsInputCode(
                    AndroidLaunchRecipe.InputProtocolJvsWmmt);
                True(player.Button3 == false && player.Button4 == true &&
                     player.Button5 == true && player.Button6 == false,
                    "WMMT second-gear sensor encoding");

                var request = JvsHelper.CraftJvsPackage(0x01, new byte[] { 0x22, 0x03 });
                var reply = JvsPackageEmulator.GetReply(request);
                Equal("E000090101FF007F003F00C8", Convert.ToHexString(reply),
                    "WMMT wheel/gas/brake JVS reply");
            }
            finally
            {
                source.ReleaseAll();
                source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsWmmt);
            }
        }

        private static void ValidateMkdxForwarding()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3,
                         ForwardedInputButton.Button4
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9034, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "MKDX forwarded " + button);
            }

            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: short.MaxValue),
                         (Axis: (ushort)5, Value: (short)16_384),
                         (Axis: (ushort)4, Value: (short)8_192)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9034, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "MKDX forwarded axis " + axis);
            }

            source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsMkdx);
            var player = InputCode.PlayerDigitalButtons[0];
            True(player.Button5 == true, "MKDX item mapping");
            True(player.Button1 == true, "MKDX menu-enter mapping");
            True(player.ExtensionButton1_2 == true, "MKDX Mario mapping");
            True(player.Button2 == true, "MKDX Banapass mapping");
            Equal(byte.MaxValue, InputCode.AnalogBytes[0], "MKDX wheel channel");
            Equal((byte)127, InputCode.AnalogBytes[2], "MKDX gas channel");
            Equal((byte)63, InputCode.AnalogBytes[4], "MKDX brake channel");
            source.ReleaseAll();
            source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsMkdx);
        }

        private static void ValidateInitialDForwarding()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9036, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Initial D forwarded " + button);
            }

            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: short.MaxValue),
                         (Axis: (ushort)5, Value: (short)16_384),
                         (Axis: (ushort)4, Value: (short)8_192)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9036, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "Initial D forwarded axis " + axis);
            }

            source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsInitialD);
            True(InputCode.PlayerDigitalButtons[0].Button1 == true,
                "Initial D view mapping");
            True(InputCode.PlayerDigitalButtons[1].Up == true,
                "Initial D shift-up mapping");
            True(InputCode.PlayerDigitalButtons[1].Down == true,
                "Initial D shift-down mapping");
            Equal((byte)0xE1, InputCode.AnalogBytes[0], "Initial D clamped wheel channel");
            Equal((byte)127, InputCode.AnalogBytes[2], "Initial D gas channel");
            Equal((byte)63, InputCode.AnalogBytes[4], "Initial D brake channel");
            source.ReleaseAll();
            source.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsInitialD);
        }

        private static void ValidateMachStormForwarding()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3,
                         ForwardedInputButton.Button4
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9028, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "MachStorm forwarded " + button);
            }

            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: short.MaxValue),
                         (Axis: (ushort)1, Value: short.MinValue),
                         (Axis: (ushort)5, Value: short.MaxValue),
                         (Axis: (ushort)4, Value: (short)16_384)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9028, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(frame[..length]),
                    "MachStorm forwarded axis " + axis);
            }

            source.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsMachStorm);
            var player = InputCode.PlayerDigitalButtons[0];
            True(player.Button1 == true, "MachStorm menu-enter mapping");
            True(player.ExtensionButton1_2 == true, "MachStorm primary weapon mapping");
            True(player.ExtensionButton1_1 == true, "MachStorm secondary weapon mapping");
            True(player.Button3 == true, "Star Wars view mapping");
            True(player.Button2 == false, "MachStorm source button 2 cleared");
            Equal((byte)191, InputCode.AnalogBytes[2], "MachStorm throttle channel");
            Equal(byte.MaxValue, InputCode.AnalogBytes[4], "MachStorm aim-X channel");
            Equal((byte)0, InputCode.AnalogBytes[6], "MachStorm aim-Y channel");
            source.ReleaseAll();

            var reversedSource = new WinlatorForwardedInputSource(reverseYAxis: true);
            var reversedLength = ForwardedInputProtocol.WriteAxisFrame(
                frame, 1, 1, 9028, 0, 1, short.MinValue, 0);
            Equal(ForwardedInputApplyResult.Applied,
                reversedSource.ApplyFrame(frame[..reversedLength]),
                "MachStorm reversed Y source");
            reversedSource.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsMachStorm);
            Equal(byte.MaxValue, InputCode.AnalogBytes[6],
                "MachStorm reversed aim-Y channel");
            reversedSource.ReleaseAll();
        }

        private static void ValidateDominantAxisCapture()
        {
            var previous = new XiGamepad();
            var verticalMovement = new XiGamepad
            {
                LeftThumbX = 9_000,
                LeftThumbY = 24_000
            };
            True(GamepadAxisCapture.TrySelectDominantThumb(
                    verticalMovement, previous, 2, out var vertical, out var verticalName),
                "dominant vertical stick capture");
            True(vertical?.IsLeftThumbY == true && vertical.IsLeftThumbX == false,
                "vertical movement is not stolen by horizontal axis noise");
            Equal("LeftThumbY+", verticalName, "vertical capture name");
            Equal(2, vertical!.XInputIndex, "vertical capture device index");

            var horizontalMovement = new XiGamepad
            {
                RightThumbX = -25_000,
                RightThumbY = -9_000
            };
            True(GamepadAxisCapture.TrySelectDominantThumb(
                    horizontalMovement, previous, 1, out var horizontal, out var horizontalName),
                "dominant horizontal stick capture");
            True(horizontal?.IsRightThumbX == true && horizontal.IsRightThumbY == false,
                "strongest horizontal movement remains selectable");
            True(horizontal!.IsAxisMinus, "negative horizontal direction retained");
            Equal("RightThumbX-", horizontalName, "horizontal capture name");
        }

        private static void ValidateSegaRingDrivingForwarding()
        {
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];

            var src = new WinlatorForwardedInputSource();
            uint sequence = 1;
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3,
                         ForwardedInputButton.Button4,
                         ForwardedInputButton.Button6
                     })
            {
                var length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9046, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied, src.ApplyFrame(frame[..length]),
                    "SRC forwarded " + button);
            }
            foreach (var (axis, value) in new[]
                     {
                         (Axis: (ushort)0, Value: short.MaxValue),
                         (Axis: (ushort)5, Value: (short)16_384),
                         (Axis: (ushort)4, Value: (short)8_192)
                     })
            {
                var length = ForwardedInputProtocol.WriteAxisFrame(
                    frame, sequence++, sequence, 9046, 0, axis, value, 0);
                Equal(ForwardedInputApplyResult.Applied, src.ApplyFrame(frame[..length]),
                    "SRC forwarded axis " + axis);
            }

            src.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsSegaRacingClassic);
            var player = InputCode.PlayerDigitalButtons[0];
            True(player.Up == true && player.Down == true &&
                 player.Left == true && player.Right == true,
                "SRC four view-switch mapping");
            True(InputCode.PlayerDigitalButtons[1].Up == false &&
                 InputCode.PlayerDigitalButtons[1].Left == true &&
                 InputCode.PlayerDigitalButtons[1].Down == true,
                "SRC shift-up enters gear two sensor state");
            Equal(byte.MaxValue, InputCode.AnalogBytes[0], "SRC wheel channel");
            Equal((byte)127, InputCode.AnalogBytes[2], "SRC gas channel");
            Equal((byte)63, InputCode.AnalogBytes[4], "SRC brake channel");
            src.ReleaseAll();

            var sonic = new WinlatorForwardedInputSource();
            sequence = 1;
            var startLength = ForwardedInputProtocol.WriteButtonFrame(
                frame, sequence++, sequence, 9047, 0, ForwardedInputButton.Start, true);
            Equal(ForwardedInputApplyResult.Applied, sonic.ApplyFrame(frame[..startLength]),
                "Ko Drive forwarded Start");
            var wheelLength = ForwardedInputProtocol.WriteAxisFrame(
                frame, sequence++, sequence, 9047, 0, 0, short.MaxValue, 0);
            Equal(ForwardedInputApplyResult.Applied, sonic.ApplyFrame(frame[..wheelLength]),
                "Ko Drive forwarded wheel");
            sonic.PublishControlsToJvsInputCode(AndroidLaunchRecipe.InputProtocolJvsSegaSonic);
            True(InputCode.PlayerDigitalButtons[0].Start == false,
                "Ko Drive clears ordinary JVS Start");
            True(InputCode.PlayerDigitalButtons[0].Button1 == true,
                "Ko Drive Start maps to Start/Item switch");
            Equal((byte)0xED, InputCode.AnalogBytes[0], "Ko Drive clamped wheel channel");
            sonic.ReleaseAll();
        }

        private static void ValidateSegaRingGunForwarding()
        {
            Span<byte> frame = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];

            var dreamRaiders = new WinlatorForwardedInputSource();
            var length = ForwardedInputProtocol.WritePointerAbsoluteFrame(
                frame, 1, 1, 9048, 0, 1,
                0, ushort.MaxValue, ushort.MaxValue, 1, 1);
            Equal(ForwardedInputApplyResult.Applied,
                dreamRaiders.ApplyFrame(frame[..length]), "Dream Raiders pointer");
            dreamRaiders.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsSegaDreamRaiders);
            True(InputCode.PlayerDigitalButtons[0].Button1 == true,
                "Dream Raiders touch trigger");
            Equal((byte)64, InputCode.AnalogBytes[0],
                "Dream Raiders complemented Y channel");
            Equal((byte)192, InputCode.AnalogBytes[2],
                "Dream Raiders complemented X channel");

            var goldenGun = new WinlatorForwardedInputSource();
            uint sequence = 1;
            length = ForwardedInputProtocol.WritePointerAbsoluteFrame(
                frame, sequence++, sequence, 9049, 0, 1,
                ushort.MaxValue, 0, 0, 2, 0);
            Equal(ForwardedInputApplyResult.Applied,
                goldenGun.ApplyFrame(frame[..length]), "Golden Gun pointer");
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button1,
                         ForwardedInputButton.Button2
                     })
            {
                length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9049, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied,
                    goldenGun.ApplyFrame(frame[..length]), "Golden Gun " + button);
            }
            goldenGun.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsSegaGoldenGun);
            True(InputCode.PlayerDigitalButtons[0].Button1 == true,
                "Golden Gun trigger");
            True(InputCode.PlayerDigitalButtons[0].Button2 == true,
                "Golden Gun reload");
            Equal((byte)250, InputCode.AnalogBytes[0],
                "Golden Gun inverted-layout X channel");
            Equal((byte)1, InputCode.AnalogBytes[2],
                "Golden Gun inverted-layout Y channel");

            var letsGoIsland = new WinlatorForwardedInputSource();
            sequence = 1;
            length = ForwardedInputProtocol.WritePointerAbsoluteFrame(
                frame, sequence++, sequence, 9050, 0, 1,
                0, 0, 1, 3, 1);
            Equal(ForwardedInputApplyResult.Applied,
                letsGoIsland.ApplyFrame(frame[..length]), "Let's Go Island pointer");
            foreach (var button in new[]
                     {
                         ForwardedInputButton.Button2,
                         ForwardedInputButton.Button3
                     })
            {
                length = ForwardedInputProtocol.WriteButtonFrame(
                    frame, sequence++, sequence, 9050, 0, button, true);
                Equal(ForwardedInputApplyResult.Applied,
                    letsGoIsland.ApplyFrame(frame[..length]), "Let's Go Island " + button);
            }
            letsGoIsland.PublishControlsToJvsInputCode(
                AndroidLaunchRecipe.InputProtocolJvsSegaLetsGoIsland);
            True(InputCode.PlayerDigitalButtons[0].Button1 == true,
                "Let's Go Island touch left trigger");
            True(InputCode.PlayerDigitalButtons[0].Button2 == true,
                "Let's Go Island right trigger");
            True(InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 == true,
                "Let's Go Island 2D/3D switch");
            Equal((byte)220, InputCode.AnalogBytes[0],
                "Let's Go Island complemented Y channel");
            Equal((byte)228, InputCode.AnalogBytes[2],
                "Let's Go Island complemented X channel");

            dreamRaiders.ReleaseAll();
            goldenGun.ReleaseAll();
            letsGoIsland.ReleaseAll();
        }

        private static void ValidateChunkedStream()
        {
            var first = new byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.ButtonPayloadBytes];
            var second = new byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.ButtonPayloadBytes];
            ForwardedInputProtocol.WriteButtonFrame(
                first, 1, 1, 400, 1, ForwardedInputButton.Start, true);
            ForwardedInputProtocol.WriteButtonFrame(
                second, 2, 2, 400, 1, ForwardedInputButton.Coin, true);
            var streamBytes = new byte[first.Length + second.Length];
            first.CopyTo(streamBytes, 0);
            second.CopyTo(streamBytes, first.Length);

            using var stream = new ChunkedReadStream(streamBytes, 3);
            var reader = new ForwardedInputStreamReader(stream);
            var source = new WinlatorForwardedInputSource();
            True(reader.ReadAndApply(source, out var firstResult), "first chunked frame");
            Equal(ForwardedInputApplyResult.Applied, firstResult, "first chunked result");
            True(reader.ReadAndApply(source, out var secondResult), "second chunked frame");
            Equal(ForwardedInputApplyResult.Applied, secondResult, "second chunked result");
            source.PublishDigitalButtonsToInputCode();
            True(InputCode.PlayerDigitalButtons[1].Start == true, "chunked start edge");
            True(InputCode.PlayerDigitalButtons[1].Coin == true, "chunked coin edge");
            False(reader.ReadAndApply(source, out _), "chunked EOF");
            source.PublishDigitalButtonsToInputCode();
            False(InputCode.PlayerDigitalButtons[1].Start == true, "EOF start release");
            False(InputCode.PlayerDigitalButtons[1].Coin == true, "EOF coin release");
        }

        private static void ValidateCancelledAsyncStream()
        {
            var source = new WinlatorForwardedInputSource();
            Span<byte> packet = stackalloc byte[
                ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.ButtonPayloadBytes];
            var length = ForwardedInputProtocol.WriteButtonFrame(
                packet, 1, 1, 500, 0, ForwardedInputButton.Start, true);
            Equal(ForwardedInputApplyResult.Applied, source.ApplyFrame(packet[..length]),
                "cancellation held edge");

            using var stream = new CancellationOnlyReadStream();
            var reader = new ForwardedInputStreamReader(stream);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                reader.ReadAndApplyAsync(source, stop.Token).AsTask().GetAwaiter().GetResult();
                throw new InvalidOperationException("cancelled asynchronous read completed normally.");
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }

            source.PublishDigitalButtonsToInputCode();
            False(InputCode.PlayerDigitalButtons[0].Start == true,
                "cancellation releases held controls");
        }

        private sealed class CancellationOnlyReadStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return 0;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();
        }

        private sealed class ChunkedReadStream : Stream
        {
            private readonly byte[] _source;
            private readonly int _maximumChunk;
            private int _offset;

            public ChunkedReadStream(byte[] source, int maximumChunk)
            {
                _source = source;
                _maximumChunk = maximumChunk;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _source.Length;
            public override long Position
            {
                get => _offset;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = _source.Length - _offset;
                if (remaining == 0)
                    return 0;
                var copied = Math.Min(Math.Min(count, _maximumChunk), remaining);
                Array.Copy(_source, _offset, buffer, offset, copied);
                _offset += copied;
                return copied;
            }

            public override int Read(Span<byte> buffer)
            {
                var remaining = _source.Length - _offset;
                if (remaining == 0)
                    return 0;
                var copied = Math.Min(Math.Min(buffer.Length, _maximumChunk), remaining);
                _source.AsSpan(_offset, copied).CopyTo(buffer);
                _offset += copied;
                return copied;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();
        }

        private static void True(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name + " did not pass.");
        }

        private static void False(bool condition, string name) => True(!condition, name);

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(
                    $"{name} mismatch: expected {expected}, got {actual}.");
        }
    }
}
