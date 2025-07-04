using System.Diagnostics;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    public static class GameErrorMessage
    {
        public static void ShowGameError(int errorCode)
        {
            switch (errorCode)
            {
                case 1337:
                    MessageBox.Show(Properties.Resources.GameError1337);
                    break;
                case 76501:
                    MessageBox.Show(Properties.Resources.GameError76501);
                    break;
                case 76502:
                    MessageBox.Show(Properties.Resources.GameError76502);
                    break;
                case 76503:
                    MessageBox.Show(Properties.Resources.GameError76503);
                    break;
                case 3820:
                    MessageBox.Show(Properties.Resources.GameError3820);
                    break;
                case 3821:
                    MessageBox.Show(Properties.Resources.GameError3821);
                    break;
                case 3822:
                    if (MessageBoxHelper.InfoYesNo(Properties.Resources.GameError3822))
                    {
                        Process.Start("https://teknoparrot.com/OnlineProfile/Highscore");
                    }
                    break;
                case 3823:
                    MessageBox.Show(Properties.Resources.GameError3823);
                    break;
                case 2820:
                    MessageBox.Show(Properties.Resources.GameError2820);
                    break;
                case 2821:
                    MessageBox.Show(Properties.Resources.GameError2821);
                    break;
                case 2822:
                    MessageBox.Show(Properties.Resources.GameError2822);
                    break;
                case 2823:
                    MessageBox.Show(Properties.Resources.GameError2823);
                    break;
                case 7688:
                    MessageBox.Show(Properties.Resources.GameError7688);
                    break;
                case 0xB0B0001:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0001);
                    break;
                case 0xB0B0002:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0002);
                    break;
                case 0xB0B0003:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0003);
                    break;
                case 0xB0B0004:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0004);
                    break;
                case 0xB0B0005:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0005);
                    break;
                case 0xB0B0006:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0006);
                    break;
                case 0xB0B0007:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0007);
                    break;
                case 0xB0B0008:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0008);
                    break;
                case 0xB0B0009:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0009);
                    break;
                case 0xB0B000A:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000A);
                    break;
                case 0xB0B000B:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000B);
                    break;
                case 0xB0B000C:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000C);
                    break;
                case 0xB0B000D:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000D);
                    break;
                case 0xB0B000E:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000E);
                    break;
                case 0xB0B000F:
                    MessageBox.Show(Properties.Resources.GameErrorB0B000F);
                    break;
                case 0xB0B0010:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0010);
                    break;
                case 0xB0B0011:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0011);
                    break;
                case 0xB0B0012:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0012);
                    break;
                case 0xB0B0013:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0013);
                    break;
                case 0xB0B0020:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0020);
                    break;
                case 0xB0B0021:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0021);
                    break;
                case 0xB0B0022:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0022);
                    break;
                case 0xB0B0023:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0023);
                    break;
                case 0xB0B0024:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0024);
                    break;
                case 0xB0B0025:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0025);
                    break;
                case 0xB0B0026:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0026);
                    break;
                case 0xB0B0027:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0027);
                    break;
                case 0xB0B0028:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0028);
                    break;
                case 0xB0B0029:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0029);
                    break;
                case 0xB0B0030:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0030);
                    break;
                case 0xB0B0031:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0031);
                    break;
                case 0xB0B0032:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0032);
                    break;
                case 0xD00D000:
                    MessageBox.Show(Properties.Resources.GameErrorD00D000);
                    break;
                case 0xB0B0033:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0033);
                    break;
                case 0xB0B0034:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0034);
                    break;
                case 0xB0B0035:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0035);
                    break;
                case 0xB0B0036:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0036);
                    break;
                case 0xB0B0037:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0037);
                    break;
                case 0xB0B0038:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0038);
                    break;
                case 0xB0B0039:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0039);
                    break;
                case 0xB0B0040:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0040);
                    break;
                case 0xB0B0041:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0041);
                    break;
                case 0xB0B0042:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0042);
                    break;
                case 0xB0B0043:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0043);
                    break;
                case 0xB0B0044:
                    MessageBox.Show(Properties.Resources.GameErrorB0B0044);
                    break;
                case 0xAAA0000:
                    MessageBox.Show(Properties.Resources.GameErrorAAA0000);
                    break;
                case 0xAAA0001:
                    MessageBox.Show(Properties.Resources.GameErrorAAA0001);
                    break;
                case 0x870030:
                    MessageBox.Show(Properties.Resources.GameError870030);
                    break;
                case 0x870031:
                    MessageBox.Show(Properties.Resources.GameError870031);
                    break;
                case 0x870032:
                    MessageBox.Show(Properties.Resources.GameError870032);
                    break;
                case 0x870033:
                    MessageBox.Show(Properties.Resources.GameError870033);
                    break;
                case 0x870034:
                    MessageBox.Show(Properties.Resources.GameError870034);
                    break;
            }
        }
    }
}
