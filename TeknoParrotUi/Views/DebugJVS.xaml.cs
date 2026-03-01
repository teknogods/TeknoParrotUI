using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for DebugJVS.xaml
    /// </summary>
    public partial class DebugJVS : Window
    {
        public bool JvsOverride;
        public DebugJVS()
        {
            InitializeComponent();
            JvsOverride = false;
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            JvsOverride = !JvsOverride;
        }

        public void StartDebugInputThread()
        {
            Thread timerThread = new Thread(DebugInputThread);
            timerThread.Start();
        }

        public void DebugInputThread()
        {
            while (true)
            {
                if (JvsOverride)
                {
                    DoCheckBoxesDude();
                }
                Thread.Sleep(16);
            }
        }

        public void DoCheckBoxesDude()
        {
            this.Dispatcher.Invoke(() =>
            {


                InputCode.PlayerDigitalButtons[0].Start = P1Start.IsChecked != null && P1Start.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Start = P2Start.IsChecked != null && P2Start.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Service = P1Service.IsChecked != null && P1Service.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Service = P2Service.IsChecked != null && P2Service.IsChecked.Value;

                InputCode.PlayerDigitalButtons[0].Up = P1Up.IsChecked != null && P1Up.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Down = P1Down.IsChecked != null && P1Down.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Left = P1Left.IsChecked != null && P1Left.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Right = P1Right.IsChecked != null && P1Right.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button1 = P1Button1.IsChecked != null && P1Button1.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button2 = P1Button2.IsChecked != null && P1Button2.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button3 = P1Button3.IsChecked != null && P1Button3.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button4 = P1Button4.IsChecked != null && P1Button4.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button5 = P1Button5.IsChecked != null && P1Button5.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].Button6 = P1Button6.IsChecked != null && P1Button6.IsChecked.Value;

                InputCode.PlayerDigitalButtons[1].Up = P2Up.IsChecked != null && P2Up.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Down = P2Down.IsChecked != null && P2Down.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Left = P2Left.IsChecked != null && P2Left.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Right = P2Right.IsChecked != null && P2Right.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button1 = P2Button1.IsChecked != null && P2Button1.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button2 = P2Button2.IsChecked != null && P2Button2.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button3 = P2Button3.IsChecked != null && P2Button3.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button4 = P2Button4.IsChecked != null && P2Button4.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button5 = P2Button5.IsChecked != null && P2Button5.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].Button6 = P2Button6.IsChecked != null && P2Button6.IsChecked.Value;

                InputCode.PlayerDigitalButtons[0].ExtensionButton1 = ExtOne1.IsChecked != null && ExtOne1.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2 = ExtOne2.IsChecked != null && ExtOne2.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton3 = ExtOne3.IsChecked != null && ExtOne3.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton4 = ExtOne4.IsChecked != null && ExtOne4.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = ExtOne11.IsChecked != null && ExtOne11.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = ExtOne12.IsChecked != null && ExtOne12.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = ExtOne13.IsChecked != null && ExtOne13.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = ExtOne14.IsChecked != null && ExtOne14.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = ExtOne15.IsChecked != null && ExtOne15.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = ExtOne16.IsChecked != null && ExtOne16.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = ExtOne17.IsChecked != null && ExtOne17.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = ExtOne18.IsChecked != null && ExtOne18.IsChecked.Value;

                InputCode.PlayerDigitalButtons[1].ExtensionButton1 = ExtTwo1.IsChecked != null && ExtTwo1.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2 = ExtTwo2.IsChecked != null && ExtTwo2.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton3 = ExtTwo3.IsChecked != null && ExtTwo3.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton4 = ExtTwo4.IsChecked != null && ExtTwo4.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 = ExtTwo11.IsChecked != null && ExtTwo11.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 = ExtTwo12.IsChecked != null && ExtTwo12.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 = ExtTwo13.IsChecked != null && ExtTwo13.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 = ExtTwo14.IsChecked != null && ExtTwo14.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 = ExtTwo15.IsChecked != null && ExtTwo15.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 = ExtTwo16.IsChecked != null && ExtTwo16.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 = ExtTwo17.IsChecked != null && ExtTwo17.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = ExtTwo18.IsChecked != null && ExtTwo18.IsChecked.Value;

                InputCode.PlayerDigitalButtons[0].ExtensionButton2_1 = ExtOne21.IsChecked != null && ExtOne21.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_2 = ExtOne22.IsChecked != null && ExtOne22.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_3 = ExtOne23.IsChecked != null && ExtOne23.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_4 = ExtOne24.IsChecked != null && ExtOne24.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_5 = ExtOne25.IsChecked != null && ExtOne25.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_6 = ExtOne26.IsChecked != null && ExtOne26.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_7 = ExtOne27.IsChecked != null && ExtOne27.IsChecked.Value;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2_8 = ExtOne28.IsChecked != null && ExtOne28.IsChecked.Value;

                InputCode.PlayerDigitalButtons[1].ExtensionButton2_1 = ExtTwo21.IsChecked != null && ExtTwo21.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_2 = ExtTwo22.IsChecked != null && ExtTwo22.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_3 = ExtTwo23.IsChecked != null && ExtTwo23.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_4 = ExtTwo24.IsChecked != null && ExtTwo24.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_5 = ExtTwo25.IsChecked != null && ExtTwo25.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_6 = ExtTwo26.IsChecked != null && ExtTwo26.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_7 = ExtTwo27.IsChecked != null && ExtTwo27.IsChecked.Value;
                InputCode.PlayerDigitalButtons[1].ExtensionButton2_8 = ExtTwo28.IsChecked != null && ExtTwo28.IsChecked.Value;

                if (NumericAnalog0.Value.HasValue)
                    InputCode.AnalogBytes[0] = (byte)NumericAnalog0.Value;
                if (NumericAnalog1.Value.HasValue)
                    InputCode.AnalogBytes[1] = (byte)NumericAnalog1.Value;
                if (NumericAnalog2.Value.HasValue)
                    InputCode.AnalogBytes[2] = (byte)NumericAnalog2.Value;
                if (NumericAnalog3.Value.HasValue)
                    InputCode.AnalogBytes[3] = (byte)NumericAnalog3.Value;
                if (NumericAnalog4.Value.HasValue)
                    InputCode.AnalogBytes[4] = (byte)NumericAnalog4.Value;
                if (NumericAnalog5.Value.HasValue)
                    InputCode.AnalogBytes[5] = (byte)NumericAnalog5.Value;
                if (NumericAnalog6.Value.HasValue)
                    InputCode.AnalogBytes[6] = (byte)NumericAnalog6.Value;
                if (NumericAnalog7.Value.HasValue)
                    InputCode.AnalogBytes[7] = (byte)NumericAnalog7.Value;
                if (NumericAnalog8.Value.HasValue)
                    InputCode.AnalogBytes[8] = (byte)NumericAnalog8.Value;
                if (NumericAnalog9.Value.HasValue)
                    InputCode.AnalogBytes[9] = (byte)NumericAnalog9.Value;
                if (NumericAnalog10.Value.HasValue)
                    InputCode.AnalogBytes[10] = (byte)NumericAnalog10.Value;
                if (NumericAnalog11.Value.HasValue)
                    InputCode.AnalogBytes[11] = (byte)NumericAnalog11.Value;
                if (NumericAnalog12.Value.HasValue)
                    InputCode.AnalogBytes[12] = (byte)NumericAnalog12.Value;
                if (NumericAnalog13.Value.HasValue)
                    InputCode.AnalogBytes[13] = (byte)NumericAnalog13.Value;
                if (NumericAnalog14.Value.HasValue)
                    InputCode.AnalogBytes[14] = (byte)NumericAnalog14.Value;
                if (NumericAnalog15.Value.HasValue)
                    InputCode.AnalogBytes[15] = (byte)NumericAnalog15.Value;

                if (NumericAnalog16.Value.HasValue)
                    InputCode.AnalogBytes[16] = (byte)NumericAnalog16.Value;
                if (NumericAnalog17.Value.HasValue)
                    InputCode.AnalogBytes[17] = (byte)NumericAnalog17.Value;
                if (NumericAnalog18.Value.HasValue)
                    InputCode.AnalogBytes[18] = (byte)NumericAnalog18.Value;
                if (NumericAnalog19.Value.HasValue)
                    InputCode.AnalogBytes[19] = (byte)NumericAnalog19.Value;
                if (NumericAnalog20.Value.HasValue)
                    InputCode.AnalogBytes[20] = (byte)NumericAnalog20.Value;

                InputCode.PlayerDigitalButtons[0].Test = TEST.IsChecked != null && TEST.IsChecked.Value;
            });
        }
    }
}
