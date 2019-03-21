using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

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

        private static readonly int[] Coins = new int[4];
        private static readonly bool[] CoinStates = new bool[4];

        public static bool Taito;
        public static bool TaitoStick;
        public static bool TaitoBattleGear;
        public static bool DualJvsEmulation;
        public static bool InvertMaiMaiButtons;

        public static void Initialize()
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
        }

        /// <summary>
        /// Gets special bits for Digital.
        /// </summary>
        /// <returns>Bits for digital.</returns>
        private static byte GetSpecialBits(int index)
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
        private static byte GetPlayerControls(int index)
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
        private static byte GetPlayerControlsExt(int index)
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

        private static byte GetPlayerControlsExt2(int index)
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

        private static byte GetPlayerControlsExt3(int index)
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
                case 0x65:
                    return JvsTaito65(reply, multiPackage);
                case 0x6A:
                    return JvsTaito6A(reply);
                case 0x6B:
                    return JvsTaito6B(reply);
                case 0x6D:
                    return JvsTaito6D(reply);
                case 0x23:
                    return JvsTaito23(reply);
                case 0x34:
                    return JvsTaito34(bytesLeft, reply);
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
                case 0x2E:
                    return JvsGetHopperReply(reply, multiPackage);
                case 0x2F:
                    return JvsReTransmitData(reply);
                case 0x30:
                case 0x31:
                    return JvsGetCoinReduce(bytesLeft, reply, multiPackage);
                case 0x32:
                    return JvsGeneralPurposeOutput(bytesLeft, reply, multiPackage);
                case 0x33:
                    return JvsAnalogOutput(bytesLeft, reply, multiPackage);
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
            if (Namco)
            {
                reply.LengthReduction = 1;
                reply.Bytes = new byte[0];
            }
            else
            {
                Console.WriteLine($"Unknown package, contact Reaver! Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
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

            reply.Bytes = !multiPackage ? new byte[] {  } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsPayoutSubtractionOutput(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 4;

            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };
            return reply;
        }

        private static JvsReply JvsTaito01(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[]
            {
                0x01, // Resolution
                0x00 // UNK
            };
            return reply;
        }

        private static JvsReply JvsTaito03(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[]{ 0x01};
            return reply;
        }

        private static JvsReply JvsTaito04(JvsReply reply)
        {
            reply.LengthReduction = 1;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito05(JvsReply reply)
        {
            reply.LengthReduction = 3;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito34(byte[] bytesLeft, JvsReply reply)
        {
            reply.LengthReduction = 2 + bytesLeft[1];
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito65(JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 2;
            reply.Bytes = multiPackage ? new byte[] { 0x01, 0x00, 0x00 } : new byte[] { 0x00, 0x00 };
            return reply;
        }

        private static JvsReply JvsTaito23(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito6A(JvsReply reply)
        {
            reply.LengthReduction = 9;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito6B(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[0];
            return reply;
        }

        private static JvsReply JvsTaito6D(JvsReply reply)
        {
            reply.LengthReduction = 2;
            reply.Bytes = new byte[0];
            return reply;
        }

        //private static JvsReply JvsTaito6F(JvsReply reply)
        //{
        //    reply.LengthReduction = 2;
        //    reply.Bytes = new byte[0];
        //    return reply;
        //}

        private static JvsReply JvsTaito70(JvsReply reply)
        {
            reply.LengthReduction = 2;
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
            reply.Bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00};
            return reply;
        }

        private static JvsReply JvsGetCoinReduce(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            reply.LengthReduction = 4;
            reply.Bytes = !multiPackage ? new byte[] { } : new byte[] { 0x01 };

            var coinSlot = bytesLeft[1];
            var coinCount = (bytesLeft[2] << 8) | bytesLeft[3];

            Coins[coinSlot] -= coinCount;

            if (Coins[coinSlot] < 0)
            {
                Coins[coinSlot] = 0;
            }

            return reply;
        }

        private static JvsReply JvsGeneralPurposeOutput(byte[] bytesLeft, JvsReply reply, bool multiPackage)
        {
            var byteCount = bytesLeft[1];
            reply.LengthReduction = byteCount + 2; // Command Code + Size + Outputs

            // Special invalid package from Virtua-R Limit
            //if(bytesLeft.Length > 4)
            //    if (bytesLeft[byteCount + 2] == 0x00)
            //        reply.LengthReduction++;

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

            if (bytesLeft[1] == 0x01 || bytesLeft[1] == 0x02)
            {
                reply.Bytes = new byte[] { };
                reply.LengthReduction = 2;
                return reply;
            }

            MessageBox.Show($"Unsupported JVS_OP_ADDRESS package, contact Reaver! Package: {JvsHelper.ByteArrayToString(bytesLeft)}");
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
            bytes.Add(0x02); // Players
            bytes.Add(JvsSwitchCount); // Buttons
            bytes.Add(0x00); // null

            bytes.Add(0x02); // IOFUNC_COINTYPE
            bytes.Add(0x02); // 2 slots
            bytes.Add(0x00); // null
            bytes.Add(0x00); // null

            bytes.Add(0x03); // IO_FUNC_ANALOGS
            bytes.Add(0x08); // channels
            bytes.Add(0x0A); // bits
            bytes.Add(0x00); // null

            bytes.Add(0x12); // IO_FUNC_GENERAL_PURPOSE_OUTPUT
            bytes.Add(0x14); // CHANNELS

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

            if (TaitoBattleGear)
            {
                byte gas = 0;
                byte brake = 0;
                if (node == 1)
                {
                    gas = InputCode.AnalogBytes[1];
                    brake = InputCode.AnalogBytes[3];
                }
                else
                {
                    gas = InputCode.AnalogBytes2[1];
                    brake = InputCode.AnalogBytes2[3];
                }

                byteLst.Add(0x04);
                byteLst.Add(gas);

                byteLst.Add(0x04);
                byteLst.Add(brake);

                byteLst.Add(0x80);
                byteLst.Add(0);

                byteLst.Add(0x80);
                byteLst.Add(0);

                byteLst.Add(0x80);
                byteLst.Add(0);

                byteLst.Add(0x80);
                byteLst.Add(0);

                byteLst.Add(0x80);
                byteLst.Add(0);

                byteLst.Add(0x80);
                byteLst.Add(0);

                reply.Bytes = byteLst.ToArray();
                return reply;
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

            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
            {
                if (!CoinStates[0])
                {
                    Coins[0] = 1;
                    CoinStates[0] = true;
                }
                else
                {
                    Coins[0] = 0;
                }
            }
            else
            {
                Coins[0] = 0;
                CoinStates[0] = false;
            }

            if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
            {
                if (!CoinStates[1])
                {
                    Coins[1] = 1;
                    CoinStates[1] = true;
                }
                else
                {
                    Coins[1] = 0;
                }
            }
            else
            {
                Coins[1] = 0;
                CoinStates[1] = false;
            }

            if (multiPackage)
                byteLst.Add(0x01);

            for (int i = 0; i < slotCount; i++)
            {
                byteLst.Add((byte)(Coins[i] >> 8));
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
                MessageBox.Show($"Why would you have more than 2 players?  Package: {JvsHelper.ByteArrayToString(bytesLeft)}", "Contact Reaver asap!",
                    MessageBoxButtons.OK, MessageBoxIcon.Question);
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
                MessageBox.Show($"Why would you have more than 2 players?  Package: {JvsHelper.ByteArrayToString(bytesLeft)}", "Contact Reaver asap!",
                    MessageBoxButtons.OK, MessageBoxIcon.Question);
                throw new NotSupportedException();
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
                if (i == 0 || i == 1 || i == 2 || i == data.Length-1)
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
                        Console.WriteLine($"Error full package: {JvsHelper.ByteArrayToString(data)}");
                        return null;
                    }
                }
                for (int x = 0; x < reply.LengthReduction; x++)
                {
                    if(byteLst.Count != 0)
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
        public static byte[] GetReply(byte[] data)
        {
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
