using System;

namespace TeknoParrotUi.Common
{
    public enum GunHorizontalDirection
    {
        HorizontalCenter,
        Up,
        Down
    }
    public enum GunVerticalDirection
    {
        VerticalCenter,
        Left,
        Right,
    }
    public enum Direction
    {
        HorizontalCenter,
        VerticalCenter,
        Left,
        Right,
        Up,
        Down,
        FFLeft,
        FFRight,
        FFUp,
        FFDown,
        FFHoriCenter,
        FFVertCenter,
        RelativeUp,
        RelativeDown,
        RelativeLeft,
        RelativeRight,
        RelativeHoriCenter,
        RelativeVertCenter
    }

    public class PokkenButtons
    {
        private bool? _up;
        private bool? _down;
        private bool? _right;
        private bool? _left;
        private bool? _buttonA;
        private bool? _buttonB;
        private bool? _buttonX;
        private bool? _buttonY;
        private bool? _buttonL;
        private bool? _buttonR;
        private bool? _start;

        public bool LeftPressed()
        {
            return Left.HasValue && Left.Value;
        }

        public bool RightPressed()
        {
            return Right.HasValue && Right.Value;
        }

        public bool UpPressed()
        {
            return Up.HasValue && Up.Value;
        }

        public bool DownPressed()
        {
            return Down.HasValue && Down.Value;
        }

        public bool? Up
        {
            get { return _up; }
            set
            {
                if (value != null) _up = value;
            }
        }

        public bool? Down
        {
            get { return _down; }
            set
            {
                if (value != null) _down = value;
            }
        }

        public bool? Left
        {
            get { return _left; }
            set
            {
                if (value != null) _left = value;
            }
        }

        public bool? Right
        {
            get { return _right; }
            set
            {
                if (value != null) _right = value;
            }
        }

        public bool? ButtonA
        {
            get { return _buttonA; }
            set
            {
                if (value != null) _buttonA = value;
            }
        }

        public bool? ButtonB
        {
            get { return _buttonB; }
            set
            {
                if (value != null) _buttonB = value;
            }
        }

        public bool? ButtonX
        {
            get { return _buttonX; }
            set
            {
                if (value != null) _buttonX = value;
            }
        }

        public bool? ButtonY
        {
            get { return _buttonY; }
            set
            {
                if (value != null) _buttonY = value;
            }
        }

        public bool? ButtonL
        {
            get { return _buttonL; }
            set
            {
                if (value != null) _buttonL = value;
            }
        }

        public bool? ButtonR
        {
            get { return _buttonR; }
            set
            {
                if (value != null) _buttonR = value;
            }
        }

        public bool? Start
        {
            get { return _start; }
            set { if (value != null) _start = value; }
        }
    }

    public class PlayerButtons
    {
        public bool LeftPressed()
        {
            return Left.HasValue && Left.Value;
        }
        public bool RightPressed()
        {
            return Right.HasValue && Right.Value;
        }
        public bool UpPressed()
        {
            return Up.HasValue && Up.Value;
        }
        public bool DownPressed()
        {
            return Down.HasValue && Down.Value;
        }
        public bool FFLeftPressed()
        {
            return Button3.HasValue && Button3.Value;
        }
        public bool FFRightPressed()
        {
            return Button4.HasValue && Button4.Value;
        }
        public bool FFUpPressed()
        {
            return Button1.HasValue && Button1.Value;
        }
        public bool FFDownPressed()
        {
            return Button2.HasValue && Button2.Value;
        }
        public bool RelativeUpPressed()
        {
            return RelativeUp.HasValue && RelativeUp.Value;
        }
        public bool RelativeDownPressed()
        {
            return RelativeDown.HasValue && RelativeDown.Value;
        }
        public bool RelativeLeftPressed()
        {
            return RelativeLeft.HasValue && RelativeLeft.Value;
        }
        public bool RelativeRightPressed()
        {
            return RelativeRight.HasValue && RelativeRight.Value;
        }
        private bool? _up;
        private bool? _down;
        private bool? _right;
        private bool? _left;
        private bool? _button1;
        private bool? _button2;
        private bool? _button3;
        private bool? _button4;
        private bool? _button5;
        private bool? _button6;
        private bool? _start;
        private bool? _service;
        private bool? _test;
        private bool? _coin;
        private bool? _extensionButton1;
        private bool? _extensionButton2;
        private bool? _extensionButton3;
        private bool? _extensionButton4;

        private bool? _extensionButton1_1;
        private bool? _extensionButton1_2;
        private bool? _extensionButton1_3;
        private bool? _extensionButton1_4;
        private bool? _extensionButton1_5;
        private bool? _extensionButton1_6;
        private bool? _extensionButton1_7;
        private bool? _extensionButton1_8;

        private bool? _extensionButton2_1;
        private bool? _extensionButton2_2;
        private bool? _extensionButton2_3;
        private bool? _extensionButton2_4;
        private bool? _extensionButton2_5;
        private bool? _extensionButton2_6;
        private bool? _extensionButton2_7;
        private bool? _extensionButton2_8;

        private bool? _relativeUp;
        private bool? _relativeDown;
        private bool? _relativeLeft;
        private bool? _relativeRight;

        public Guid JoystickGuid { get; set; }

        public bool? Up
        {
            get { return _up; }
            set { if(value != null) _up = value; }
        }

        public bool? Down
        {
            get { return _down; }
            set { if (value != null) _down = value; }
        }

        public bool? Left
        {
            get { return _left; }
            set { if (value != null) _left = value; }
        }

        public bool? Right
        {
            get { return _right; }
            set { if (value != null) _right = value; }
        }

        public bool? Button1
        {
            get { return _button1; }
            set { if (value != null) _button1 = value; }
        }

        public bool? Button2
        {
            get { return _button2; }
            set { if (value != null) _button2 = value; }
        }

        public bool? Button3
        {
            get { return _button3; }
            set { if (value != null) _button3 = value; }
        }

        public bool? Button4
        {
            get { return _button4; }
            set { if (value != null) _button4 = value; }
        }

        public bool? Button5
        {
            get { return _button5; }
            set { if (value != null) _button5 = value; }
        }

        public bool? Button6
        {
            get { return _button6; }
            set { if (value != null) _button6 = value; }
        }

        public bool? ExtensionButton1
        {
            get { return _extensionButton1; }
            set { if (value != null) _extensionButton1 = value; }
        }

        public bool? ExtensionButton2
        {
            get { return _extensionButton2; }
            set { if (value != null) _extensionButton2 = value; }
        }

        public bool? ExtensionButton3
        {
            get { return _extensionButton3; }
            set { if (value != null) _extensionButton3 = value; }
        }

        public bool? ExtensionButton4
        {
            get { return _extensionButton4; }
            set { if (value != null) _extensionButton4 = value; }
        }

        public bool? ExtensionButton1_1
        {
            get { return _extensionButton1_1; }
            set { if (value != null) _extensionButton1_1 = value; }
        }

        public bool? ExtensionButton1_2
        {
            get { return _extensionButton1_2; }
            set { if (value != null) _extensionButton1_2 = value; }
        }

        public bool? ExtensionButton1_3
        {
            get { return _extensionButton1_3; }
            set { if (value != null) _extensionButton1_3 = value; }
        }
        public bool? ExtensionButton1_4
        {
            get { return _extensionButton1_4; }
            set { if (value != null) _extensionButton1_4 = value; }
        }
        public bool? ExtensionButton1_5
        {
            get { return _extensionButton1_5; }
            set { if (value != null) _extensionButton1_5 = value; }
        }
        public bool? ExtensionButton1_6
        {
            get { return _extensionButton1_6; }
            set { if (value != null) _extensionButton1_6 = value; }
        }
        public bool? ExtensionButton1_7
        {
            get { return _extensionButton1_7; }
            set { if (value != null) _extensionButton1_7 = value; }
        }
        public bool? ExtensionButton1_8
        {
            get { return _extensionButton1_8; }
            set { if (value != null) _extensionButton1_8 = value; }
        }

        public bool? ExtensionButton2_1
        {
            get { return _extensionButton2_1; }
            set { if (value != null) _extensionButton2_1 = value; }
        }

        public bool? ExtensionButton2_2
        {
            get { return _extensionButton2_2; }
            set { if (value != null) _extensionButton2_2 = value; }
        }

        public bool? ExtensionButton2_3
        {
            get { return _extensionButton2_3; }
            set { if (value != null) _extensionButton2_3 = value; }
        }
        public bool? ExtensionButton2_4
        {
            get { return _extensionButton2_4; }
            set { if (value != null) _extensionButton2_4 = value; }
        }
        public bool? ExtensionButton2_5
        {
            get { return _extensionButton2_5; }
            set { if (value != null) _extensionButton2_5 = value; }
        }
        public bool? ExtensionButton2_6
        {
            get { return _extensionButton2_6; }
            set { if (value != null) _extensionButton2_6 = value; }
        }
        public bool? ExtensionButton2_7
        {
            get { return _extensionButton2_7; }
            set { if (value != null) _extensionButton2_7 = value; }
        }
        public bool? ExtensionButton2_8
        {
            get { return _extensionButton2_8; }
            set { if (value != null) _extensionButton2_8 = value; }
        }

        public bool? Start
        {
            get { return _start; }
            set { if (value != null) _start = value; }
        }

        public bool? Service
        {
            get { return _service; }
            set { if (value != null) _service = value; }
        }

        public bool? Test
        {
            get { return _test; }
            set { if (value != null) _test = value; }
        }

        public bool? Coin
        {
            get { return _coin; }
            set { if (value != null) _coin = value; }
        }

        public bool? RelativeUp
        {
            get { return _relativeUp; }
            set { if (value != null) _relativeUp = value; }
        }
        public bool? RelativeDown
        {
            get { return _relativeDown; }
            set { if (value != null) _relativeDown = value; }
        }
        public bool? RelativeLeft
        {
            get { return _relativeLeft; }
            set { if (value != null) _relativeLeft = value; }
        }
        public bool? RelativeRight
        {
            get { return _relativeRight; }
            set { if (value != null) _relativeRight = value; }
        }
    }
    public static class InputCode
    {
        public static void SetPlayerDirection(PlayerButtons playerButtons, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    playerButtons.Up = true;
                    playerButtons.Down = false;
                    break;
                case Direction.Down:
                    playerButtons.Up = false;
                    playerButtons.Down = true;
                    break;
                case Direction.VerticalCenter:
                    playerButtons.Up = false;
                    playerButtons.Down = false;
                    break;
                case Direction.HorizontalCenter:
                    playerButtons.Left = false;
                    playerButtons.Right = false;
                    break;
                case Direction.Left:
                    playerButtons.Left = true;
                    playerButtons.Right = false;
                    break;
                case Direction.Right:
                    playerButtons.Left = false;
                    playerButtons.Right = true;
                    break;
                case Direction.FFUp:
                    playerButtons.Button1 = true;
                    playerButtons.Button2 = false;
                    break;
                case Direction.FFDown:
                    playerButtons.Button1 = false;
                    playerButtons.Button2 = true;
                    break;
                case Direction.FFHoriCenter:
                    playerButtons.Button3 = false;
                    playerButtons.Button4 = false;
                    break;
                case Direction.FFVertCenter:
                    playerButtons.Button1 = false;
                    playerButtons.Button2 = false;
                    break;
                case Direction.FFLeft:
                    playerButtons.Button3 = true;
                    playerButtons.Button4 = false;
                    break;
                case Direction.FFRight:
                    playerButtons.Button3 = false;
                    playerButtons.Button4 = true;
                    break;
                case Direction.RelativeHoriCenter:
                    playerButtons.RelativeLeft = false;
                    playerButtons.RelativeRight = false;
                    break;
                case Direction.RelativeVertCenter:
                    playerButtons.RelativeDown = false;
                    playerButtons.RelativeUp = false;
                    break;
                case Direction.RelativeUp:
                    playerButtons.RelativeDown = false;
                    playerButtons.RelativeUp = true;
                    break;
                case Direction.RelativeDown:
                    playerButtons.RelativeUp = false;
                    playerButtons.RelativeDown = true;
                    break;
                case Direction.RelativeLeft:
                    playerButtons.RelativeRight = false;
                    playerButtons.RelativeLeft = true;
                    break;
                case Direction.RelativeRight:
                    playerButtons.RelativeLeft = false;
                    playerButtons.RelativeRight = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static void SetPlayerDirection(PokkenButtons pokkenButtons, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    pokkenButtons.Up = true;
                    pokkenButtons.Down = false;
                    break;
                case Direction.Down:
                    pokkenButtons.Up = false;
                    pokkenButtons.Down = true;
                    break;
                case Direction.VerticalCenter:
                    pokkenButtons.Up = false;
                    pokkenButtons.Down = false;
                    break;
                case Direction.HorizontalCenter:
                    pokkenButtons.Left = false;
                    pokkenButtons.Right = false;
                    break;
                case Direction.Left:
                    pokkenButtons.Left = true;
                    pokkenButtons.Right = false;
                    break;
                case Direction.Right:
                    pokkenButtons.Left = false;
                    pokkenButtons.Right = true;
                    break;
                case Direction.FFDown:
                    break;
                case Direction.FFUp:
                    break;
                case Direction.FFLeft:
                    break;
                case Direction.FFRight:
                    break;
                case Direction.FFVertCenter:
                    break;
                case Direction.FFHoriCenter:
                    break;
                case Direction.RelativeUp:
                    break;
                case Direction.RelativeDown:
                    break;
                case Direction.RelativeLeft:
                    break;
                case Direction.RelativeRight:
                    break;
                case Direction.RelativeHoriCenter:
                    break;
                case Direction.RelativeVertCenter:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static EmulationProfile ButtonMode { get; set; }

        public static byte[] AnalogBytes = new byte[32];

        public static byte[] AnalogBytes2 = new byte[32];

        public static void SetAnalogByte(int index, byte? value, bool jvsTwo = false)
        {
            if (value == null)
                return;

            if (!jvsTwo)
            {
                AnalogBytes[index] = value.Value;
            }
            else
            {
                AnalogBytes2[index] = value.Value;
            }
        }

        public static PlayerButtons[] PlayerDigitalButtons = new PlayerButtons[4]
        {
            new PlayerButtons(),
            new PlayerButtons(),
            new PlayerButtons(),
            new PlayerButtons(),
        };

        public static PokkenButtons PokkenInputButtons = new PokkenButtons();
    }
}
