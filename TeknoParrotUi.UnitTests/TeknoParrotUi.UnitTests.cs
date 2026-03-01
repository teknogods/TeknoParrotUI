using System;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using Xunit;

namespace TeknoParrotUi.UnitTests
{
    public class TeknoParrotUiUnitTests
    {
        // TODO: WRITE UNIT TESTS FOR EVERY SINGLE JVS CASE, INCLUDING AMOUNT OF SWITCHES / ANALOGS in queries etc.
        [Theory]
        [InlineData(3000)]
        [InlineData(-32767)]
        public void DoTheDualAxisFun(int pedal)
        {
            // Arrange
            var di = new InputListenerDirectInput();

            // Act
            var gas = di.HandleGasBrakeForJvs(pedal, false, false, false, true);
            var brake = di.HandleGasBrakeForJvs(pedal, true, false, false, false);

            // Assert
        }

        [Fact]
        public void JVS_RESET_ShouldReturnNothing()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_RESET, 0xD9 });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Empty(reply);
        }

        [Fact]
        public void JVS_ADDRESS_ShouldReturnPackage()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_ADDRESS, 0x01 }); // 0x01 = Bus address
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] {});

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_ADDRESS_2_ShouldReturnPackage()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_ADDRESS, 0x02 }); // 0x01 = Bus address
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GET_IDENTIFIER_ShouldReturnIdentifier()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.ID_DATA });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, Encoding.ASCII.GetBytes(JVSIdentifiers.Sega2005Jvs14572));

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GET_ANALOG_ShouldReturnThreeChannels()
        {
            // Arrange
            InputCode.AnalogBytes[0] = 0xBA;
            InputCode.AnalogBytes[2] = 0xBE;
            InputCode.AnalogBytes[4] = 0xBE;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.ANALOG, 0x03 }); // 22 = REQUEST ANALOG, 3 = 3 Channels
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { (byte)InputCode.AnalogBytes[0], 0x00, (byte)InputCode.AnalogBytes[2], 0x00, (byte)InputCode.AnalogBytes[4], 0x00 });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOneAndPlayerTwoButtonsAndExt()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Button4 = true;
            InputCode.PlayerDigitalButtons[1].Button1 = true;
            InputCode.PlayerDigitalButtons[1].Button4 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x02, 0x02 }); // 22 = REQUEST DIGITAL, 2 = Player Count, 2 Bytes Per Player
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40, 0x02, 0x40 }); // Special Switches, P1, P1Ext, P2, P2Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_READ_DIGINAL_READ_ANALOG_ShouldReturnPlayerOneAndPlayerTwoButtonsAndExtAndThreeAnalogChannels()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Button4 = true;
            InputCode.PlayerDigitalButtons[1].Button1 = true;
            InputCode.PlayerDigitalButtons[1].Button4 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            InputCode.AnalogBytes[0] = 0xBA;
            InputCode.AnalogBytes[2] = 0xBE;
            InputCode.AnalogBytes[4] = 0xBE;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x02, 0x02, (byte)JVSRead.ANALOG, 0x03 }); // 22 = REQUEST DIGITAL, 2 = Player Count, 2 Bytes Per Player, 22 = REQUEST ANALOG, 3 = 3 Channels
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40, 0x02, 0x40, 0x01, (byte)InputCode.AnalogBytes[0], 0x00, (byte)InputCode.AnalogBytes[2], 0x00, (byte)InputCode.AnalogBytes[4], 0x00 }); // Special Switches, P1, P1Ext, P2, P2Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOneAndPlayerTwoButtons()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[1].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x02, 0x01 }); // 22 = REQUEST DIGITAL, 2 = Player Count, 1 Bytes Per Player
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x02 }); // Special Switches, P1, P2

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOne()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x01, 0x01 }); // 22 = REQUEST DIGITAL, 1 = Player Count, 1 Bytes Per Player
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02 }); // Special Switches, P1

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOneExt()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Button4 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x01, 0x02 }); // 22 = REQUEST DIGITAL, 1 = Player Count, 2 Bytes Per Player
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40 }); // Special Switches, P1, P1Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Theory]
        [InlineData(1, new byte[] { (byte)JVSCoin.NORMAL, 0x00} )]
        [InlineData(2, new byte[] { (byte)JVSCoin.NORMAL, 0x00, (byte)JVSCoin.NORMAL, 0x00 })]
        public void JVS_READ_COIN_ShouldReturnOneCoinSlotWithOkStatus(byte slots, byte[] expected)
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new[] { (byte)JVSRead.COIN, slots }); // 22 = Request coin slots, 1 slot
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, expected); // Coin Normal Operation, 0 coins inserted.

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GET_COMMANDREV_ShouldReturnVersionOnePointThree()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x11 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x13 }); // Revision 1.3

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }


        [Fact]
        public void JVS_GET_JVSVERSION_ShouldReturnVersionTwoPointZero()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x12 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x20 }); // Version 2.0

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GET_COMMUNICATIONVERSION_ShouldReturnVersionTwoPointZero()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x13 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x10 }); // Version 1.0

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GET_COMMUNICATIONVERSION_JVSVERSION_COMMANDREV_ShouldReturnRightValues()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x13, 0x12, 0x11 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x10, 0x01, 0x20, 0x01, 0x13 });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GENERALPURPOSEOUTPUT_ShouldReturnJVSOK_REPORTOK()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x32, 0x02, 0x00, 0x00 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GENERALPURPOSEOUTPUT2_ShouldReturnJVSOK_REPORTOK()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x32, 0x03, 0x00, 0x00, 0x00 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.Equal(reply.Length, espectedBytes.Length);
            Assert.True(reply.SequenceEqual(espectedBytes));
        }

        [Fact]
        public void JVS_GETMULTIPLEPACKAGES_ShouldReturnJVSOK_REPORTOK()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { 0x20, 0x02, 0x02, 0x32, 0x03, 0x00, 0x00, 0x00, 0x30, 0x01, 0xf0, 0x0d, 0x21, 0x02, 0x22, 0x08 });
            var espectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);
            string str = "";
            foreach (var b in reply)
            {
                str += b.ToString("X2") + " ";
            }
            Console.WriteLine(reply);
        }

        [Theory]
        [InlineData(0, 0x57, 0x57, 0xA7, false)]
        [InlineData(65535, 0xA7, 0x57, 0xA7, false)]
        [InlineData(32767, 0x7F, 0x57, 0xA7, false)]
        [InlineData(-32767, 0x57, 0x57, 0xA7, true)]
        [InlineData(32767, 0xA7, 0x57, 0xA7, true)]
        [InlineData(0, 0x7F, 0x57, 0xA7, true)]
        public void JVS_Test_Analog_LindberghInitialD_Range(int wheelValue, int expectedResult, int minJvs, int maxJvs, bool isXInput)
        {
            var result = JvsHelper.CalculateWheelPos(wheelValue, isXInput, false, minJvs, maxJvs);

            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(0, 0x6B, 0x6B, 0x93, false)]
        [InlineData(65535, 0x93, 0x6B, 0x93, false)]
        [InlineData(32767, 0x7F, 0x6B, 0x93, false)]
        [InlineData(-32767, 0x6B, 0x6B, 0x93, true)]
        [InlineData(32767, 0x93, 0x6B, 0x93, true)]
        [InlineData(0, 0x7F, 0x6B, 0x93, true)]
        public void JVS_Test_Analog_RingEdgeInitialD_Range(int wheelValue, int expectedResult, int minJvs, int maxJvs, bool isXInput)
        {
            var result = JvsHelper.CalculateWheelPos(wheelValue, isXInput, false, minJvs, maxJvs);

            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(0, 0, 0, 0xFF, false)]
        [InlineData(65535, 0xFF, 0, 0xFF, false)]
        [InlineData(32767, 0x7F, 0, 0xFF, false)]
        [InlineData(-32767, 0, 0, 0xFF, true)]
        [InlineData(32767, 0xFE, 0, 0xFF, true)]
        [InlineData(0, 0x7F, 0, 0xFF, true)]
        public void JVS_Test_Analog_Normal_Range(int wheelValue, int expectedResult, int minJvs, int maxJvs, bool isXInput)
        {
            var result = JvsHelper.CalculateWheelPos(wheelValue, isXInput, false, minJvs, maxJvs);

            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(0, 0x1D, 0x1D, 0xED, false)]
        [InlineData(65535, 0xED, 0x1D, 0xED, false)]
        [InlineData(32767, 0x85, 0x1D, 0xED, false)]
        [InlineData(-32767, 0x1D, 0x1D, 0xED, true)]
        [InlineData(32767, 0xED, 0x1D, 0xED, true)]
        [InlineData(0, 0x85, 0x1D, 0xED, true)]
        public void JVS_Test_Analog_Sonic_Range(int wheelValue, int expectedResult, int minJvs, int maxJvs, bool isXInput)
        {
            var result = JvsHelper.CalculateWheelPos(wheelValue, isXInput, false, minJvs, maxJvs);

            Assert.Equal(expectedResult, result);
        }
    }
}
