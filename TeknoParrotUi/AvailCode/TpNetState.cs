using System;

namespace TeknoParrotUi.AvailCode
{
    public static class TpNetStateStruct
    {
        public unsafe struct TpNetState
        {
            public UInt64 lobbyId;
            public UInt64 steamId;
            public UInt64 hostId;
            public int numMembers;
            public fixed ulong members[32];
        }
    }
}
