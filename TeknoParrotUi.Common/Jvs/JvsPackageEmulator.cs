using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SharpDX.DirectInput;

namespace TeknoParrotUi.Common.Jvs
{
    public class JvsReply
    {
        public byte[] Bytes { get; set; }
        public int LengthReduction { get; set; }

        public bool Error { get; set; }
    }

    public static class JvsPackageEmulator
    {
        public static byte JvsCommVersion;
        public static byte JvsVersion;
        public static byte JvsCommandRevision;
        public static byte JvsSwitchCount;
        public static string JvsIdentifier;
        public static bool Namco;

        public static int[] Coins = new int[4];           // need to be able to change this directly from input handlers
        public static bool[] CoinStates = new bool[4];    // and we need this to detect changes.

        public static bool Taito;
        public static bool TaitoStick;
        public static bool LetsGoSafari;
        public static bool TaitoBattleGear;
        public static bool DualJvsEmulation;
        public static bool InvertMaiMaiButtons;
        public static bool ProMode;
        public static bool Hotd4;
        public static bool Xiyangyang;
        public static byte[] PrevAnalog = new byte[7];

        public static char BattleGearKeyBreakChar;

        private static GameProfile _gameProfile;

        private static bool _proModeShiftSen1 = true;    // 6MT sensor, Start in 6MT state to skip game mech switch
        private static bool _proModeShiftSen2 = false;   // Sequential sensor

        private static bool _superMonkeyBallTicketOutput = false;
        private static bool _superMonkeyBallTicketState = false;
        private static bool _superMonkeyBallTicketToggled = false;
        private static bool _isSuperMonkeyBall = false;

        public static void Initialize(GameProfile gameProfile)
        {
            JvsCommVersion = 0x10;
            JvsVersion = 0x20;
            JvsCommandRevision = 0x13;
            JvsSwitchCount = 0x0E;
            JvsIdentifier = JVSIdentifiers.Sega2005Jvs14572;
            Namco = false;
            Taito = false;
            TaitoStick = false;
            TaitoBattleGear = false;
            DualJvsEmulation = false;
            LetsGoSafari = false;
            ProMode = false;
            Hotd4 = false;
            Xiyangyang = false;
            BattleGearKeyBreakChar = 'T';

            _gameProfile = gameProfile;

            // Reset Super Monkey Ball specific variables
            _isSuperMonkeyBall = gameProfile?.ProfileName == "Mballblitz";
            _superMonkeyBallTicketOutput = false;
            _superMonkeyBallTicketState = false;
            _superMonkeyBallTicketToggled = false;
        }

        private static bool GetSuperMonkeyBallTicketState(int index)
        {
            if (_isSuperMonkeyBall && index == 0 && _superMonkeyBallTicketOutput)
            {
                // Only toggle once per JVS packet cycle
                if (!_superMonkeyBallTicketToggled)
                {
                    _superMonkeyBallTicketState = !_superMonkeyBallTicketState;
                    _superMonkeyBallTicketToggled = true;
                }
                
                return _superMonkeyBallTicketState;
            }
            return false;
        }

        /// <summary>
        /// Gets special bits for Digital.
        /// </summary>
        /// <returns>Bits for digital.</returns>
        public static byte GetSpecialBits(int index)
        {
            byte result = 00;
            if (InputCode.PlayerDigitalButtons[index].Test.HasValue && InputCode.PlayerDigitalButtons[index].Test.Value)
                result |= 0x80;
            return result;
        }

        /// <summary>
        /// Gets Player 1 switch data.
        /// </summary>
        /// <returns>Bits for player 1 switch data.</returns>
        private static byte GetPlayerControlsInvertMaiMai(int index)
        {
            byte result = 0;
            if (InputCode.PlayerDigitalButtons[index].Start.HasValue && InputCode.PlayerDigitalButtons[index].Start.Value)
                result |= 0x80;
            if (InputCode.PlayerDigitalButtons[index].Service.HasValue && InputCode.PlayerDigitalButtons[index].Service.Value)
                result |= 0x40;
            if (!InputCode.PlayerDigitalButtons[index].UpPressed())
                result |= 0x20;
            if (!InputCode.PlayerDigitalButtons[index].DownPressed())
                result |= 0x10;
            if (!InputCode.PlayerDigitalButtons[index].LeftPressed())
                result |= 0x08;
            if (!InputCode.PlayerDigitalButtons[index].RightPressed())
                result |= 0x04;
            if (!(InputCode.PlayerDigitalButtons[index].Button1.HasValue && InputCode.PlayerDigitalButtons[index].Button1.Value))
                result |= 0x02;
            if (!(InputCode.PlayerDigitalButtons[index].Button2.HasValue && InputCode.PlayerDigitalButtons[index].Button2.Value))
                result |= 0x01;
            return result;
        }

        /// <summary>
        /// Gets Player 1 switch data.
        /// </summary>
        /// <returns>Bits for player 1 switch data.</returns>
        public static byte GetPlayerControls(int index)
        {
            byte result = 0;
            if (InputCode.PlayerDigitalButtons[index].Start.HasValue && InputCode.PlayerDigitalButtons[index].Start.Value)
                result |= 0x80;
            if (InputCode.PlayerDigitalButtons[index].Service.HasValue && InputCode.PlayerDigitalButtons[index].Service.Value)
                result |= 0x40;
            if (InputCode.PlayerDigitalButtons[index].UpPressed())
                result |= 0x20;
            if (InputCode.PlayerDigitalButtons[index].DownPressed())
                result |= 0x10;
            if (InputCode.PlayerDigitalButtons[index].LeftPressed())
                result |= 0x08;
            if (InputCode.PlayerDigitalButtons[index].RightPressed())
                result |= 0x04;
            if (InputCode.PlayerDigitalButtons[index].Button1.HasValue && InputCode.PlayerDigitalButtons[index].Button1.Value)
                result |= 0x02;
            if (InputCode.PlayerDigitalButtons[index].Button2.HasValue && InputCode.PlayerDigitalButtons[index].Button2.Value)
                result |= 0x01;

            // Super Monkey Ball Blitz ticket dispenser emulation
            if (GetSuperMonkeyBallTicketState(index))
            {
                // Whenever the dispenser actually dispenses a ticket, it briefly flicks p1buttonright to tell the game
                // hey, I just dispensed a ticket! Otherwise people playing in ticket mode constantly get a "TICKET JAM"
                // error in game.
                result |= 0x04; // Set P1ButtonRight bit
            }

            return result;
        }


        /// <summary>
        /// Gets Player 1 extended switch data.
        /// </summary>
        /// <returns>Bits for player 1 extended switch data.</returns>
        private static byte GetPlayerControlsExtInvertMaiMai(int index)
        {
            byte result = 0;
            if (!(InputCode.PlayerDigitalButtons[index].Button3.HasValue && InputCode.PlayerDigitalButtons[index].Button3.Value))
                result |= 0x80;
            if (!(InputCode.PlayerDigitalButtons[index].Button4.HasValue && InputCode.PlayerDigitalButtons[index].Button4.Value))
                result |= 0x40;
            if (!(InputCode.PlayerDigitalButtons[index].Button5.HasValue && InputCode.PlayerDigitalButtons[index].Button5.Value))
                result |= 0x20;
            if (!(InputCode.PlayerDigitalButtons[index].Button6.HasValue && InputCode.PlayerDigitalButtons[index].Button6.Value))
                result |= 0x10;
            if (!(InputCode.PlayerDigitalButtons[index].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton4.Value))
                result |= 0x08;
            if (!(InputCode.PlayerDigitalButtons[index].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton3.Value))
                result |= 0x04;
            if (!(InputCode.PlayerDigitalButtons[index].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2.Value))
                result |= 0x02;
            if (!(InputCode.PlayerDigitalButtons[index].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1.Value))
                result |= 0x01;
            return result;
        }

        /// <summary>
        /// Gets Player 1 extended switch data.
        /// </summary>
        /// <returns>Bits for player 1 extended switch data.</returns>
        public static byte GetPlayerControlsExt(int index)
        {
            byte result = 0;
            if (InputCode.PlayerDigitalButtons[index].Button3.HasValue && InputCode.PlayerDigitalButtons[index].Button3.Value)
                result |= 0x80;
            if (InputCode.PlayerDigitalButtons[index].Button4.HasValue && InputCode.PlayerDigitalButtons[index].Button4.Value)
                result |= 0x40;
            if (InputCode.PlayerDigitalButtons[index].Button5.HasValue && InputCode.PlayerDigitalButtons[index].Button5.Value)
                result |= 0x20;
            if (InputCode.PlayerDigitalButtons[index].Button6.HasValue && InputCode.PlayerDigitalButtons[index].Button6.Value)
                result |= 0x10;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton4.Value)
                result |= 0x08;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton3.Value)
                result |= 0x04;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2.Value)
                result |= 0x02;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1.Value)
                result |= 0x01;
            return result;
        }

        public static byte GetPlayerControlsExt2(int index)
        {
            byte result = 0;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_1.HasValue &&
                InputCode.PlayerDigitalButtons[index].ExtensionButton1_1.Value)
            {
                result |= 0x01;
            }
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_2.Value)
                result |= 0x02;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_3.Value)
                result |= 0x04;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_4.Value)
                result |= 0x08;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_5.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_5.Value)
                result |= 0x10;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_6.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_6.Value)
                result |= 0x20;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_7.Value)
                result |= 0x40;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton1_8.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton1_8.Value)
                result |= 0x80;
            return result;
        }

        public static byte GetPlayerControlsExt3(int index)
        {
            byte result = 0;
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_1.HasValue &&
                InputCode.PlayerDigitalButtons[index].ExtensionButton2_1.Value)
            {
                result |= 0x01;
            }
            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_2.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_2.Value)
                result |= 0x02;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_3.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_3.Value)
                result |= 0x04;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_4.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_4.Value)
                result |= 0x08;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_5.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_5.Value)
                result |= 0x10;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_6.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_6.Value)
                result |= 0x20;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_7.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_7.Value)
                result |= 0x40;

            if (InputCode.PlayerDigitalButtons[index].ExtensionButton2_8.HasValue && InputCode.PlayerDigitalButtons[index].ExtensionButton2_8.Value)
                result |= 0x80;
            return result;
        }

        public static void UpdateCoinCount(int index)
        {
            if ((InputCode.PlayerDigitalButtons[index].Coin != null) && (CoinStates[index] != InputCode.PlayerDigitalButtons[index].Coin))
            {
                // update state to match the switch
                CoinStates[index] = (bool)InputCode.PlayerDigitalButtons[index].Coin;
                if (!CoinStates[index])
                {
                    Coins[index]++; // increment the coin counter if coin button was released
                }
            }
        }

        public static JvsReply ParsePackage(byte[] bytesLeft, bool multiPackage, byte node)
        {
            JvsReply reply = new JvsReply();
            // We take first byte of the package
            switch (bytesLeft[0])
            {
                case (byte)JVSPacket.OP_ADDRESS:
                    return JvsGetAddress(bytesLeft, reply);
                case 0x01:
                    return JvsTaito01(reply);
                case 0x03:
                    return JvsTaito03(reply);
                case 0x04:
                    return JvsTaito04(reply);
                case 0x05:
                    return JvsTaito05(reply);
                case 0x25:
                    return JvsTaito25(reply);
                case 0x65:
                    return JvsTaito65(reply, multiPackage);
                case 0x6A:
                    return JvsTaito6A(bytesLeft, reply);
                case 0x6B:
                    return JvsTaito6B(reply);
                case 0x6D:
                    return JvsTaito6D(reply);
                case 0x23:
                    return JvsTaito23(reply);
                case 0x10:
                    return JvsGetIdentifier(reply);
                case 0x11:
                    return JvsGetCommandRev(reply, multiPackage);
                case 0x12:
                    return JvsGetJvsVersion(reply, multiPackage);
                case 0x13:
                    return JvsGetCommunicationVersion(reply, multiPackage);
                case 0x14:
                    return JvsGetSlaveFeatures(reply, multiPackage);
                case 0x15:
                    return JvsConveyMainBoardId(bytesLeft, reply);
                case 0x20:
                    if (InvertMaiMaiButtons)
                    {
                        return JvsGetDigitalReplyInvertMaiMai(bytesLeft, reply, multiPackage, node);
                    }
                    else
                    {
                        return JvsGetDigitalReply(bytesLeft, reply, multiPackage, node);
                    }
                case 0x21:
                    return JvsGetCoinReply(bytesLeft, reply, multiPackage);
                case 0x22:
                    return JvsGetAnalogReply(bytesLeft, reply, multiPackage, node);
                case 0x26:
                    return JvsGetMiscSwitchInput(bytesLeft, reply, multiPackage, node);
                case 0x2E:
                    return JvsGetHopperReply(reply, multiPackage);
                case 0x2F:
                    return JvsReTransmitData(reply);
                case 0x30:
                case 0x31:
                    return JvsGetCoinReduce(bytesLeft, reply, multiPackage);
                case 0x32:
                    return JvsGeneralPurposeOutput(bytesLeft, reply, multiPackage, node);
                case 0x33:
                    return JvsAnalogOutput(bytesLeft, reply, multiPackage);
                case 0x34:
                    return JvsCharacterOutput(bytesLeft, reply, multiPackage);
                case 0x36:
                    return JvsPayoutSubtractionOutput(reply, multiPackage);
                case 0x37:
                    return JvsGeneralPurposeOutput2(reply, multiPackage);
                case 0x70:
                    if (Taito || TaitoStick || TaitoBattleGear)
                    {
                        return JvsTaito70(reply);
                    }
                    else
                        return JvsGetNamcoCustomCommands(bytesLeft, reply, multiPackage);
                case 0x78:
                case 0x79:
                case 0x7A:
                case 0x7B:
                case 0x7C:
                case 0x7D:
                case 0x7E:
                case 0x7F:
                case 0x80:
                    return SkipNamcoUnknownCustom(reply);
            }
            if (TaitoBattleGear)
            {
                // No evidence of any of these commands apart from 0x66 and 0x6F, shall leave others just in case. Not even sure if they are taito specific for some of them?
                // Would move these to main switch but may be worth testing with other games before doing this.
                switch (bytesLeft[0])
                {
                    case 0x00:
                        return JvsTaito00(reply);
                    case 0x02:
                        return JvsTaito02(reply);
                    case 0x40:
                        return JvsTaito40(reply);
                    // Below called in pro mode for unknown reason
                    case 0x66:
                        return JvsTaito66(reply);
                    // Key/RFID related command
                    case 0x6F:
                        return JvsTaito6F(reply);
                    case 0x26:
                        return JvsTaito26(reply);
                    case 0xFF:
                        return JvsTaitoFF(reply);
                }
            }
            if (Namco)
            {
                reply.LengthReduction = 1;
                reply.Bytes = new byte[0];
            }
            else
            {
                Debug.WriteLine($"Unknown package, contact Reaver! Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
                reply.Error = true;
            }
            return reply;
        }

        private static JvsReply JvsAnalogOutput(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            var byteCount = bytesLeft[1];
            reply.LengthReduction = (byteCount * 2) + 2; // Channels + Command size

            // Special invalid package from Virtua-R Limit
            //if(bytesLeft.Length > 4)
            //    if (bytesLeft[byteCount + 2] == 0x00)
            //        reply.LengthReduction++;

            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsCharacterOutput(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            var byteCount = bytesLeft[1];
            reply.LengthReduction = byteCount + 2; // Channels + Command size

            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsPayoutSubtractionOutput(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 4;

            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsTaito00(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito01(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[]
            {
                0x01, // Resolution
                0x01 // UNK
            };
            return reply;
        }

        private static JvsReply JvsTaito02(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito03(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsTaito04(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito25(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito05(JvsReply reply)
        {
            reply.LengthReduction = 3;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito26(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito65(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 2;
            reply.Bytes = multiPackage ? new byte[] { 0x01, 0xA0 } : new byte[] { 0xA0 };   // Seconds to watchdog reset? Not really sure. Taken from real IO.
            return reply;
        }

        private static JvsReply JvsTaito23(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito40(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito66(JvsReply reply)
        {
            // Used in BG4 pro mode, not sure what it expects back?
            reply.LengthReduction = 3;
            reply.Bytes = new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsTaito6A(byte[] bytesLeft, JvsReply reply)    // Read UID? Same as response in 70
        {
            reply.LengthReduction = 9;
            reply.Bytes = new byte[] { 0x01 };  // Just needs report
            // I think this returns something to us fetched in 70
            return reply;
        }

        private static JvsReply JvsTaito6B(JvsReply reply)  // Read tag user data area
        {
            reply.LengthReduction = 1;

            var buf = Enumerable.Repeat((byte)0xFF, 0x2D).ToArray();  // Response from key reader, 0x2C+1 length
            buf[0] = 0x01;  // Report

            var keyIdBuf = new byte[7];
            // Below try is here to catch edge cases where the key id file is unlocked
            // This should hopefully be fairly rare edge case, and is better than throwing an exception potentially disrupting JVS communications
            try
            {
                using (var fs = File.Open($@"{Path.GetDirectoryName(_gameProfile.GamePath)}\OpenParrot\KeyId.txt", FileMode.OpenOrCreate))
                {
                    var read = fs.Read(keyIdBuf, 0, keyIdBuf.Length);
                    if (read != 7)
                    {
                        JvsHelper.GenerateBG4KeyID(keyIdBuf, BattleGearKeyBreakChar);
                        fs.SetLength(0);
                        fs.Write(keyIdBuf, 0, keyIdBuf.Length);
                    }
                }
            }
            catch
            {
                // This causes a "you cannot use a BG3 key" error. Not ideal but the game blatantly ignores error reports and tries to read the data anyways
                reply.Bytes = buf;
                return reply;
            }

            // NOTE: BG3 keys are different layout, the first 2 characters are stored as ASCII however the number is stored as a 16 bit integer.
            // There is also no "W_OK" baked into the tag at the end. BG4 keys have a space as the first byte before the Key ID starts.
            buf[1] = 0x20;  // Always a space at beginning of key ID, its actually 8 bytes in memory
            Buffer.BlockCopy(keyIdBuf, 0, buf, 2, keyIdBuf.Length);
            Encoding.ASCII.GetBytes("W_OK", 0, 4, buf, 41); // For the sake of accuracy, this is baked into the rfid data at the end

            reply.Bytes = buf;
            return reply;
        }

        private static JvsReply JvsTaito6D(JvsReply reply)      // Status packet
        {
            reply.LengthReduction = 1;  // Command code only
            reply.Bytes = new byte[] { 0x01, 0x00 };  // Report, Stauts
            return reply;
        }

        private static JvsReply JvsTaito6F(JvsReply reply)    // Send read command?
        {
            reply.LengthReduction = 1;  // Command code only
            // Real JVS sometimes returns "Busy" report for this, I guess when its connecting to reader?
            reply.Bytes = new byte[] { 0x01 };  // Just report required
            return reply;
        }

        private static JvsReply JvsTaito70(JvsReply reply)  // Write UID? Not really sure as UID should be read only
        {
            reply.LengthReduction = 1;
            // TODO: I believe the below is a Philips I.CODE UID. We should probably emulate this at some point (even though I dont think BG4 cares)
            reply.Bytes = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };  // 4 byte value at start, seems to be UID serial number?
                                                                                                // Last byte might be apart of this to form the 5-byte serial from the I.CODE spec
                                                                                                // Last byte MUST be >0 otherwise game errors
            return reply;
        }

        private static JvsReply JvsTaitoFF(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsGetNamcoCustomCommands(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            var subCommand = bytesLeft[1];
            switch (subCommand)
            {
                case 0x18:
                    reply.Bytes = !multiPackage ? new byte[] { 0x01 } : new byte[] { 0x01, 0x01 };
                    reply.LengthReduction = bytesLeft.Length;
                    break;
                case 0x05:
                    reply.Bytes = !multiPackage ? new byte[] { 0x01 } : new byte[] { 0x01, 0x01 };
                    reply.LengthReduction = bytesLeft[2];
                    break;
                case 0x03:
                    reply.LengthReduction = 4;
                    reply.Bytes = !multiPackage ? new byte[] { 0x00 } : new byte[] { 0x01, 0x00 };
                    break;
                case 0x15:
                    reply.LengthReduction = 4;
                    reply.Bytes = !multiPackage ? new byte[] { 0x01 } : new byte[] { 0x01, 0x01 };
                    break;
                case 0x16:
                    reply.LengthReduction = 4;
                    reply.Bytes = !multiPackage ? new byte[] { 0x01 } : new byte[] { 0x01, 0x01 };
                    break;
                default:
                    //Console.WriteLine($"Unknown namco sub command, contact Reaver! Package: 0x{subCommand.ToString("X")}");
                    break;
            }
            return reply;
        }

        private static JvsReply SkipNamcoUnknownCustom(JvsReply reply)
        {
            //if (bytesLeft[0] == 0x78 && bytesLeft[1] != 00)
            //    reply.LengthReduction = 19;
            //else
            reply.LengthReduction = 15;
            reply.Bytes = new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsReTransmitData(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
            return reply;
        }

        private static JvsReply JvsGetCoinReduce(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 4;
            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };

            var coinSlot = bytesLeft[1];
            var coinCount = (bytesLeft[2] << 8) | bytesLeft[3];
            coinSlot--; // jvs slot numbers start at 1, but we start at zero.
                        // TODO: handle dual board properly.

            if (coinSlot < Coins.Length)
            {
                Coins[coinSlot] -= coinCount;
                if (Coins[coinSlot] < 0)
                {
                    Coins[coinSlot] = 0;
                }
            }
            return reply;
        }

        private static JvsReply JvsGeneralPurposeOutput(byte[] bytesLeft, JvsReply reply, bool multiPackage, byte node)
        {
            var byteCount = bytesLeft[1];
            reply.LengthReduction = byteCount + 2; // Command Code + Size + Outputs

            var channels = new byte[byteCount];
            Buffer.BlockCopy(bytesLeft, 2, channels, 0, byteCount);

            if (ProMode && node == 2)
            {
                // Detects if motor is active, and if so, flips states between 6MT/sequential
                if (channels[0] > 0x00)
                {
                    _proModeShiftSen1 = !_proModeShiftSen1;
                    _proModeShiftSen2 = !_proModeShiftSen2;
                }
            }

            // Super Monkey Ball Blitz ticket dispenser emulation
            if (_isSuperMonkeyBall && channels.Length > 0)
            {
                // Whenever a ticket needs to be dispensed, the game sets bit 6 here
                _superMonkeyBallTicketOutput = (channels[0] & 0x40) != 0;
            }

            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsGeneralPurposeOutput2(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 3; // Command Code + Size + Outputs
            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsGetAddress(byte[] bytesLeft, JvsReply reply)
        {
            if (!DualJvsEmulation)
            {
                JvsHelper.StateView?.Write(0, 1);
            }
            else
            {
                if (bytesLeft[1] == 0x02)
                {
                    JvsHelper.StateView?.Write(0, 1);
                }
            }

            if (bytesLeft[1] == 0x01 || bytesLeft[1] == 0x02 || bytesLeft[1] == 0x07)
            {
                reply.Bytes = new byte[] { };
                reply.LengthReduction = 2;
                return reply;
            }

            Debug.WriteLine($"Unsupported JVS_OP_ADDRESS package, contact Reaver! Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
            throw new NotSupportedException();
        }

        private static JvsReply JvsGetIdentifier(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = Encoding.ASCII.GetBytes(JvsIdentifier);
            return reply;
        }

        private static JvsReply JvsGetCommunicationVersion(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 1;
            reply.Bytes = multiPackage ? new byte[] { 0x01, JvsCommVersion } : new byte[] { JvsCommVersion };
            return reply;
        }

        private static JvsReply JvsGetJvsVersion(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 1;
            reply.Bytes = multiPackage ? new byte[] { 0x01, JvsVersion } : new byte[] { JvsVersion };
            return reply;
        }

        private static JvsReply JvsGetCommandRev(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 1;
            reply.Bytes = multiPackage ? new byte[] { 0x01, JvsCommandRevision } : new byte[] { JvsCommandRevision };
            return reply;
        }

        private static JvsReply JvsGetSlaveFeatures(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 1;
            List<byte> bytes = new List<byte>();

            if (TaitoBattleGear)
            {

                if (multiPackage)
                    bytes.Add(01);
                bytes.Add(0x01); // IOFUNC_SWINPUT
                bytes.Add(0x02); // Players
                bytes.Add(0x14); // Buttons
                bytes.Add(0x00); // null

                bytes.Add(0x03); // IO_FUNC_ANALOGS
                bytes.Add(0x08); // channels
                bytes.Add(16); // bits
                bytes.Add(0x00); // null

                bytes.Add(0x02); // IOFUNC_COINTYPE
                bytes.Add(0x02); // 2 slots
                bytes.Add(0x00); // null
                bytes.Add(0x00); // null

                bytes.Add(0x00); // exit code
                bytes.Add(0x00); // null
                bytes.Add(0x00); // null
                bytes.Add(0x00); // null
                reply.Bytes = bytes.ToArray();
                return reply;
            }

            if (TaitoStick)
            {
                reply.Bytes = multiPackage
                    ? new byte[]
                    {
                        0x01, 0x01, 0x02, 0x10, 0x00, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                    }
                    : new byte[]
                    {
                        0x01, 0x02, 0x10, 0x00, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                    };
                return reply;
            }

            if (multiPackage)
                bytes.Add(01);

            bytes.Add(0x01); // IOFUNC_SWINPUT
            bytes.Add(LetsGoSafari || Xiyangyang ? (byte)0x01 : (byte)0x02);

            bytes.Add(JvsSwitchCount); // Buttons
            bytes.Add(0x00); // null

            bytes.Add(0x02); // IOFUNC_COINTYPE
            bytes.Add(0x02); // 2 slots
            bytes.Add(0x00); // null
            bytes.Add(0x00); // null

            bytes.Add(0x03); // IO_FUNC_ANALOGS
            bytes.Add(Xiyangyang ? (byte)0x00 : (byte)0x08); // channels
            bytes.Add(Xiyangyang ? (byte)0x00 : (byte)0x0A); // bits
            bytes.Add(0x00); // null

            bytes.Add(0x12); // IO_FUNC_GENERAL_PURPOSE_OUTPUT
            if (LetsGoSafari || Xiyangyang)
            {
                bytes.Add(0x10); // CHANNELS
            }
            else
            {
                bytes.Add(0x14); // CHANNELS
            }

            bytes.Add(0x00); // exit code
            bytes.Add(0x00); // null
            bytes.Add(0x00); // null

            reply.Bytes = bytes.ToArray();
            return reply;
        }

        private static JvsReply JvsConveyMainBoardId(byte[] bytesLeft, JvsReply reply)
        {
            for (var i = 0; i < bytesLeft.Length; i++)
            {
                if (bytesLeft[i] == 0x00)
                    break;
                reply.LengthReduction++;
            }
            reply.LengthReduction++;
            reply.Bytes = new byte[]
            {
                0x01, 0x01, 0x05
            };
            return reply;
        }

        private static JvsReply JvsGetAnalogReply(byte[] bytesLeft, JvsReply reply, bool multiPackage, byte node)
        {
            var byteLst = new List<byte>();
            int channelCount = bytesLeft.Length == 1 ? 8 : bytesLeft[1]; // Stupid hack for Virtua-R Limit
            reply.LengthReduction = 2;

            if (multiPackage)
                byteLst.Add(0x01);

            if (Hotd4)
            {
                // P1 shake
                InputCode.AnalogBytes[8] = (byte)(128 - Math.Min(Math.Abs(InputCode.AnalogBytes[0] - PrevAnalog[0]) * 3, 128));
                InputCode.AnalogBytes[10] = (byte)(128 - Math.Min(Math.Abs(InputCode.AnalogBytes[2] - PrevAnalog[2]) * 3, 128));

                // P2 shake
                InputCode.AnalogBytes[12] = (byte)(128 - Math.Min(Math.Abs(InputCode.AnalogBytes[4] - PrevAnalog[4]) * 3, 128));
                InputCode.AnalogBytes[14] = (byte)(128 - Math.Min(Math.Abs(InputCode.AnalogBytes[6] - PrevAnalog[6]) * 3, 128));

                Array.Copy(InputCode.AnalogBytes, PrevAnalog, 7);
            }

            for (int i = 0; i < channelCount; i++)
            {
                if (node == 1)
                {
                    byteLst.Add(InputCode.AnalogBytes[(i * 2)]);
                    byteLst.Add(InputCode.AnalogBytes[(i * 2) + 1]);
                }
                else
                {
                    byteLst.Add(InputCode.AnalogBytes2[(i * 2)]);
                    byteLst.Add(InputCode.AnalogBytes2[(i * 2) + 1]);
                }
            }
            reply.Bytes = byteLst.ToArray();
            return reply;
        }

        private static JvsReply JvsGetMiscSwitchInput(byte[] bytesLeft, JvsReply reply, bool multiPackage, byte node)
        {
            // Stub for BG4 tuned pro to make command response correct
            var byteLst = new List<byte>();
            int byteCount = bytesLeft[1];
            reply.LengthReduction = 2;

            if (multiPackage)
                byteLst.Add(0x01);

            var inputs = new byte[byteCount];

            if (ProMode && node == 2)   // is bg4 tuned pro and from IO Plus?
            {
                // Pro mode shifter mech state (Inverted by game)
                if (!_proModeShiftSen1) // 6MT
                    inputs[0] |= 0x20;
                if (!_proModeShiftSen2) // Sequential
                    inputs[0] |= 0x10;

                // Left/right shift sensors
                if (InputCode.PlayerDigitalButtons[1].Button3 == true ||
                    InputCode.PlayerDigitalButtons[1].Button4 == true)
                    inputs[0] |= 0x80;
                if (InputCode.PlayerDigitalButtons[1].UpPressed() ||
                    InputCode.PlayerDigitalButtons[1].DownPressed())
                    inputs[0] |= 0x40;
            }

            byteLst.AddRange(inputs);

            reply.Bytes = byteLst.ToArray();
            return reply;
        }

        private static JvsReply JvsGetHopperReply(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 2;

            reply.Bytes = multiPackage ? new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00 } : new byte[] { 0x00, 0x00, 0x00, 0x00 };

            return reply;
        }

        private static JvsReply JvsGetCoinReply(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            var slotCount = bytesLeft[1];
            reply.LengthReduction = 2;

            var byteLst = new List<byte>();
            // no longer need to mess with Coin and CoinStates here

            if (multiPackage)
                byteLst.Add(0x01);

            for (int i = 0; i < slotCount; i++)
            {
                byteLst.Add((byte)(Coins[i] >> 8)); // we are ignoring the actual CoinStates here, and saying things are normal 
                                                    // technically we should apply the proper OR mask based on CoinStates[i]
                                                    // here, but those only ever happen with actual arcades. :)
                byteLst.Add((byte)(Coins[i] & 0xFF));
            }

            reply.Bytes = byteLst.ToArray();

            return reply;
        }

        private static JvsReply JvsGetDigitalReplyInvertMaiMai(byte[] bytesLeft, JvsReply reply, bool multiPackage, byte node)
        {
            var baseAddr = 0;
            if (node == 2)
                baseAddr = 2;
            var byteLst = new List<byte>();
            var players = bytesLeft[1];
            var bytesToRead = bytesLeft[2];
            if (multiPackage)
                byteLst.Add(0x01);
            byteLst.Add(GetSpecialBits(0));
            if (players > 2)
            {
                Debug.WriteLine($"Why would you have more than 2 players? Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
                throw new NotSupportedException();
            }
            if (TaitoStick)
            {
                byteLst.Add(GetPlayerControlsInvertMaiMai(baseAddr));
                byteLst.Add(GetPlayerControlsExtInvertMaiMai(baseAddr));
                byteLst.Add(GetPlayerControlsInvertMaiMai(baseAddr + 1));
                byteLst.Add(GetPlayerControlsExtInvertMaiMai(baseAddr + 1));
                reply.LengthReduction = 3;
                reply.Bytes = byteLst.ToArray();
                return reply;
            }
            if (players != 0)
            {
                byteLst.Add(GetPlayerControlsInvertMaiMai(baseAddr));
                bytesToRead--;
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExtInvertMaiMai(baseAddr));
                    bytesToRead--;
                }
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExt2(baseAddr));
                    bytesToRead--;
                }
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExt3(baseAddr));
                    bytesToRead--;
                }
                while (bytesToRead != 0)
                {
                    byteLst.Add(0x00);
                    bytesToRead--;
                }
                if (players == 2)
                {
                    bytesToRead = bytesLeft[2];
                    byteLst.Add(GetPlayerControlsInvertMaiMai(baseAddr + 1));
                    bytesToRead--;
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExtInvertMaiMai(baseAddr + 1));
                        bytesToRead--;
                    }
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExt2(baseAddr + 1));
                        bytesToRead--;
                    }
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExt3(baseAddr + 1));
                        bytesToRead--;
                    }
                    while (bytesToRead != 0)
                    {
                        byteLst.Add(0x00);
                        bytesToRead--;
                    }
                }
            }
            reply.LengthReduction = 3;
            reply.Bytes = byteLst.ToArray();
            return reply;
        }

        private static JvsReply JvsGetDigitalReply(byte[] bytesLeft, JvsReply reply, bool multiPackage, byte node)
        {
            var baseAddr = 0;
            if (node == 2)
                baseAddr = 2;
            var byteLst = new List<byte>();
            var players = bytesLeft[1];
            var bytesToRead = bytesLeft[2];
            if (multiPackage)
                byteLst.Add(0x01);
            byteLst.Add(GetSpecialBits(0));
            if (players > 2)
            {
                Debug.WriteLine($"Why would you have more than 2 players? Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
                throw new NotSupportedException();
            }
            // Below is far from ideal, but bg4 pro needs some custom inputs
            if (TaitoBattleGear &&
                players == 1 &&
                bytesToRead == 3)
            {
                var btn0 = GetPlayerControls(baseAddr);
                var btn1 = GetPlayerControlsExt(baseAddr);
                var btn2 = GetPlayerControlsExt2(baseAddr);
                if (ProMode)
                {
                    // Update based on non-mapped values
                    if (InputCode.PlayerDigitalButtons[1].Button3 == true ||
                        InputCode.PlayerDigitalButtons[1].UpPressed())
                        btn1 |= 0x80;
                    if (InputCode.PlayerDigitalButtons[1].Button4 == true ||
                        InputCode.PlayerDigitalButtons[1].DownPressed())
                        btn0 |= 0x01;

                    // Flip shift up/down
                    btn0 ^= 0x01;
                    btn1 ^= 0x80;
                }
                byteLst.Add(btn0);
                byteLst.Add(btn1);
                byteLst.Add(btn2);

                reply.LengthReduction = 3;
                reply.Bytes = byteLst.ToArray();
                return reply;
            }
            if (TaitoStick)
            {
                byteLst.Add(GetPlayerControls(baseAddr));
                byteLst.Add(GetPlayerControlsExt(baseAddr));
                byteLst.Add(GetPlayerControls(baseAddr + 1));
                byteLst.Add(GetPlayerControlsExt(baseAddr + 1));
                reply.LengthReduction = 3;
                reply.Bytes = byteLst.ToArray();
                return reply;
            }
            if (players != 0)
            {
                byteLst.Add(GetPlayerControls(baseAddr));
                bytesToRead--;
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExt(baseAddr));
                    bytesToRead--;
                }
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExt2(baseAddr));
                    bytesToRead--;
                }
                if (bytesToRead != 0)
                {
                    byteLst.Add(GetPlayerControlsExt3(baseAddr));
                    bytesToRead--;
                }
                while (bytesToRead != 0)
                {
                    byteLst.Add(0x00);
                    bytesToRead--;
                }
                if (players == 2)
                {
                    bytesToRead = bytesLeft[2];
                    byteLst.Add(GetPlayerControls(baseAddr + 1));
                    bytesToRead--;
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExt(baseAddr + 1));
                        bytesToRead--;
                    }
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExt2(baseAddr + 1));
                        bytesToRead--;
                    }
                    if (bytesToRead != 0)
                    {
                        byteLst.Add(GetPlayerControlsExt3(baseAddr + 1));
                        bytesToRead--;
                    }
                    while (bytesToRead != 0)
                    {
                        byteLst.Add(0x00);
                        bytesToRead--;
                    }
                }
            }
            reply.LengthReduction = 3;
            reply.Bytes = byteLst.ToArray();
            return reply;
        }

        private static byte[] AdnvacedJvs(byte[] data)
        {
            // Disect package (take out unwanted data)
            var byteLst = new List<byte>();
            var multiPackage = false;
            var replyBytes = new List<byte>();
            var packageSize = data[2] - 1; // Reduce CRC as we don't need that
            for (int i = 0; i < data.Length; i++)
            {
                if (i == 0 || i == 1 || i == 2 || i == data.Length - 1)
                    continue;
                byteLst.Add(data[i]);
            }
            for (var i = 0; i < packageSize;)
            {
                var reply = ParsePackage(byteLst.ToArray(), multiPackage, data[1]);
                if (!Namco)
                {
                    if (reply.Error)
                    {
                        Debug.WriteLine($"Error full package: {JvsHelper.ByteArrayToString(data)}");
                        return null;
                    }
                }
                for (int x = 0; x < reply.LengthReduction; x++)
                {
                    if (byteLst.Count != 0)
                        byteLst.RemoveAt(0);
                }
                i += reply.LengthReduction;
                replyBytes.AddRange(reply.Bytes);
                multiPackage = true;
            }
            return replyBytes.ToArray();
        }

        /// <summary>
        /// THIS CODE IS BEYOND RETARTED AND FOR HACKY TESTS ONLY!!!
        /// For proper JVS handling: must code proper detection of packages, multiple requests in one package and proper responses!
        /// Now we just know what SEGA asks and return back like bunch of monkey boys.
        /// Feel free to improve.
        /// </summary>
        /// <param name="data">Input data from the com port.</param>
        /// <returns>"proper" response.</returns>
        private static int counta = -1;
        public static byte[] GetReply(byte[] data)
        {
            // Reset Super Monkey Ball ticket toggle flag at the start of each JVS packet
            _superMonkeyBallTicketToggled = false;
            
            // We don't care about these kind of packages, need to improve in case comes with lot of delay etc.
            if (data.Length <= 3)
                return new byte[0];
            Debug.WriteLine("Package: " + JvsHelper.ByteArrayToString(data));
            if (data.Length > 1 && data[1] != 0xFF)
            {
                if (!DualJvsEmulation)
                {
                    if (data[1] > 0x01)
                    {
                        return new byte[0];
                    }
                }
                else
                {
                    if (data[1] > 0x02)
                    {
                        return new byte[0];
                    }
                }
            }

            // Weird request to Type X
            if (data[0] == 0xE0 && data[1] == 0x00 && data[2] == 0x04 && data[3] == 0x01 && data[4] == 0x01 &&
                data[5] == 0x08 && data[6] == 0x0E)
            {
                counta++;
                if (counta == 0 && counta == 1 && counta == 2 && counta == 3)
                    return JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x01 });
                else
                    return JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x00 });
            }

            switch (data[3])
            {
                // E0FF03F0D9CB
                case (byte)JVSPacket.OP_RESET:
                    {
                        JvsHelper.StateView?.Write(0, 0);
                        return new byte[0];
                    }
                default:
                    {
                        var reply = JvsHelper.CraftJvsPackageWithStatusAndReport(0, AdnvacedJvs(data));
                        Debug.WriteLine("Reply: " + JvsHelper.ByteArrayToString(reply));
                        return reply;
                    }
            }
        }
    }
}
