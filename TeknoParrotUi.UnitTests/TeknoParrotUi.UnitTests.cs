using System;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using NUnit.Framework;

namespace TeknoParrotUi.UnitTests
{
    [TestFixture]
    public class TeknoParrotUiUnitTests
    {
        // TODO: WRITE UNIT TESTS FOR EVERY SINGLE JVS CASE, INCLUDING AMOUNT OF SWITCHES / ANALOGS in queries etc.

        [TestCase(3000)]
        [TestCase(-32767)]
        public void DoTheDualAxisFun(int pedal)
        {
            // Arrange
            var di = new InputListenerDirectInput();

            // Act
            var gas = di.HandleGasBrakeForJvs(pedal, false, false, false, true);
            var brake = di.HandleGasBrakeForJvs(pedal, true, false, false, false);

            // Assert
            Assert.Pass(); // Replace with actual assertions
        }

        [Test]
        public void JVS_RESET_ShouldReturnNothing()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_RESET, 0xD9 });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.IsEmpty(reply);
        }

        [Test]
        public void JVS_ADDRESS_ShouldReturnPackage()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_ADDRESS, 0x01 }); // 0x01 = Bus address
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
        public void JVS_ADDRESS_2_ShouldReturnPackage()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage((byte)JVSPacket.BROADCAST, new byte[] { (byte)JVSPacket.OP_ADDRESS, 0x02 }); // 0x01 = Bus address
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
        public void JVS_GET_IDENTIFIER_ShouldReturnIdentifier()
        {
            // Arrange
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.ID_DATA });
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, Encoding.ASCII.GetBytes(JVSIdentifiers.Sega2005Jvs14572));

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
        public void JVS_GET_ANALOG_ShouldReturnThreeChannels()
        {
            // Arrange
            InputCode.AnalogBytes[0] = 0xBA;
            InputCode.AnalogBytes[2] = 0xBE;
            InputCode.AnalogBytes[4] = 0xBE;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.ANALOG, 0x03 }); // 22 = REQUEST ANALOG, 3 = 3 Channels
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { (byte)InputCode.AnalogBytes[0], 0x00, (byte)InputCode.AnalogBytes[2], 0x00, (byte)InputCode.AnalogBytes[4], 0x00 });

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOneAndPlayerTwoButtonsAndExt()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Button4 = true;
            InputCode.PlayerDigitalButtons[1].Button1 = true;
            InputCode.PlayerDigitalButtons[1].Button4 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x02, 0x02 }); // 22 = REQUEST DIGITAL, 2 = Player Count, 2 Bytes Per Player
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40, 0x02, 0x40 }); // Special Switches, P1, P1Ext, P2, P2Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
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
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40, 0x02, 0x40, 0x01, (byte)InputCode.AnalogBytes[0], 0x00, (byte)InputCode.AnalogBytes[2], 0x00, (byte)InputCode.AnalogBytes[4], 0x00 }); // Special Switches, P1, P1Ext, P2, P2Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }

        [Test]
        public void JVS_READ_DIGINAL_ShouldReturnPlayerOneAndPlayerTwoButtons()
        {
            // Arrange
            InputCode.PlayerDigitalButtons[0].Button1 = true;
            InputCode.PlayerDigitalButtons[0].Button4 = true;
            InputCode.PlayerDigitalButtons[1].Button1 = true;
            InputCode.PlayerDigitalButtons[1].Button4 = true;
            InputCode.PlayerDigitalButtons[0].Test = true;
            var requestBytes = JvsHelper.CraftJvsPackage(1, new byte[] { (byte)JVSRead.DIGITAL, 0x02, 0x02 }); // 22 = REQUEST DIGITAL, 2 = Player Count, 2 Bytes Per Player
            var expectedBytes = JvsHelper.CraftJvsPackageWithStatusAndReport(0, new byte[] { 0x80, 0x02, 0x40, 0x02, 0x40 }); // Special Switches, P1, P1Ext, P2, P2Ext

            // Act
            var reply = JvsPackageEmulator.GetReply(requestBytes);

            // Assert
            Assert.NotNull(reply);
            Assert.AreEqual(reply.Length, expectedBytes.Length);
            Assert.IsTrue(reply.SequenceEqual(expectedBytes));
        }
    }
}