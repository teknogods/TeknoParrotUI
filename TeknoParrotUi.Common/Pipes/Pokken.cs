using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class Pokken : ControlSender
    {
        public override void Transmit()
        {
            if (InputCode.PokkenInputButtons.Up.HasValue && InputCode.PokkenInputButtons.Up.Value)
                Control |= 0x01;
            if (InputCode.PokkenInputButtons.Down.HasValue && InputCode.PokkenInputButtons.Down.Value)
                Control |= 0x02;
            if (InputCode.PokkenInputButtons.Left.HasValue && InputCode.PokkenInputButtons.Left.Value)
                Control |= 0x04;
            if (InputCode.PokkenInputButtons.Right.HasValue && InputCode.PokkenInputButtons.Right.Value)
                Control |= 0x08;
            if (InputCode.PokkenInputButtons.Start.HasValue && InputCode.PokkenInputButtons.Start.Value)
                Control |= 0x10;
            if (InputCode.PokkenInputButtons.ButtonA.HasValue && InputCode.PokkenInputButtons.ButtonA.Value)
                Control |= 0x2000;
            if (InputCode.PokkenInputButtons.ButtonB.HasValue && InputCode.PokkenInputButtons.ButtonB.Value)
                Control |= 0x1000;
            if (InputCode.PokkenInputButtons.ButtonX.HasValue && InputCode.PokkenInputButtons.ButtonX.Value)
                Control |= 0x8000;
            if (InputCode.PokkenInputButtons.ButtonY.HasValue && InputCode.PokkenInputButtons.ButtonY.Value)
                Control |= 0x4000;
            if (InputCode.PokkenInputButtons.ButtonL.HasValue && InputCode.PokkenInputButtons.ButtonL.Value)
                Control |= 0x100;
            if (InputCode.PokkenInputButtons.ButtonR.HasValue && InputCode.PokkenInputButtons.ButtonR.Value)
                Control |= 0x200;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
