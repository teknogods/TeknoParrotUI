using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDL2;

namespace ForceFeedbackJesus
{
    public static class BasicInformation
    {
        public static List<string> GetHapticDevices()
        {
            List<string> hapticList = new List<string>();
            SDL.SDL_Init(SDL.SDL_INIT_HAPTIC | SDL.SDL_INIT_JOYSTICK);
            int numHaptics = SDL.SDL_NumHaptics();

            for (int i = 0; i < numHaptics; i++)
            {
                hapticList.Add(SDL.SDL_HapticName(i));
            }
            SDL.SDL_Quit();
            return hapticList;
        }
    }
}
