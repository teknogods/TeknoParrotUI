using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class NesicaButton : ControlSender
    {
        public override void Transmit()
        {
            int nesicaControl = 0;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.Value)
                nesicaControl |= 0x01;

            JvsHelper.StateView.Write(8, nesicaControl);
        }
    }
}
