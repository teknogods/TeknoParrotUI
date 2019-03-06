using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Jvs
{
    //TODO: More organization / better names for these
    public enum JVSPacket : byte
    {
        BROADCAST = 0xFF,
        OP_RESET = 0xF0,
        OP_ADDRESS = 0xF1,
        SYNC_CODE = 0xE0,
        TRUE = 0x01,
        ADDR_MASTER = 0x00,
        COMMAND_REV = 0x13,
    }
    public enum JVSReport : byte
    {
        OK = 0x01,
        ERROR1 = 0x02,
        ERROR2 = 0x03,
        DEVICE_BUSY = 0x04,
    }
    public enum JVSStatus : byte
    {
        OK = 0x01,
        UNKNOWN = 0x02,
        CHECKSUM_FAIL = 0x03,
        OVERFLOW = 0x04
    }
    public enum JVSRead : byte
    {
        //NOTE: ID does not mean Initial D
        ID_DATA = 0x10,
        DIGITAL = 0x20,
        COIN = 0x21,
        ANALOG = 0x22,
        ROTATORY = 0x23,
    }
    public enum JVSCoin : byte
    {
        NORMAL = 0x00,
        JAM = 0x01,
        DISCONNECTED = 0x02,
        BUSY = 0x03,
    }
}
