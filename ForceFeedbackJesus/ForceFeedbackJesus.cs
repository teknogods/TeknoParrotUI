using System;
using SDL2;

namespace ForceFeedbackJesus
{
    public class ForceFeedbackJesus
    {
        private int _effectLeftId;
        private int _effectRightId;
        private int _effectFrictionId;
        private int _effectSpringId;
        private int _effectSineId;
        private IntPtr _haptic;
        private bool _thrustmasterfix;

        private Int16 _constantBase;
        private Int16 _sineBase;
        private Int16 _frictionBase;
        private Int16 _springBase;
        private IntPtr _joystick;

        public void StopRollEffects()
        {
            SDL.SDL_HapticStopEffect(_haptic, _effectLeftId);
            SDL.SDL_HapticStopEffect(_haptic, _effectRightId);
        }

        public void Uninitialize()
        {
            SDL.SDL_HapticStopEffect(_haptic, _effectLeftId);
            SDL.SDL_HapticStopEffect(_haptic, _effectRightId);
            SDL.SDL_HapticStopEffect(_haptic, _effectFrictionId);
            SDL.SDL_HapticStopEffect(_haptic, _effectSpringId);
            SDL.SDL_HapticStopEffect(_haptic, _effectSineId);
            SDL.SDL_HapticClose(_haptic);
            SDL.SDL_JoystickClose(_joystick);
            SDL.SDL_Quit();
        }

        public void TriggerRightRollEffect(float strength)
        {
            if (_thrustmasterfix)
                TriggerConstantEffect(_effectRightId, 1, strength);
            else
                TriggerConstantEffect(_effectRightId, -1, strength);
        }

        public void TriggerLeftRollEffect(float strength)
        {
            if (_thrustmasterfix)
                TriggerConstantEffect(_effectLeftId, -1, strength);
            else
                TriggerConstantEffect(_effectLeftId, 1, strength);
        }

        private void TriggerConstantEffect(int effectId, int direction, float strength)
        {
            SDL.SDL_HapticStopEffect(_haptic, _effectLeftId);
            SDL.SDL_HapticStopEffect(_haptic, _effectRightId);

            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_CONSTANT;
            tempEffect.constant.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
            unsafe
            {
                tempEffect.constant.direction.dir[0] = direction;

            }
            tempEffect.constant.length = 120;
            tempEffect.constant.delay = 0;

            if (_thrustmasterfix)
            {
                if (direction == 1)
                {
                    tempEffect.constant.level = (short)(0x10000 - (strength * _constantBase));
                }
                if (direction == -1)
                {
                    tempEffect.constant.level = (short)(strength * _constantBase);
                }
            }
            else
            {
                tempEffect.constant.level = (short)(strength * _constantBase);
            }

            SDL.SDL_HapticUpdateEffect(_haptic, effectId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, effectId, 1);
        }

        public void TriggerSineEffectInfinite(float strength)
        {
            // TODO: IMPLEMENT!
            SDL.SDL_HapticStopEffect(_haptic, _effectSineId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_SINE;
            tempEffect.periodic.direction.type = SDL.SDL_HAPTIC_CARTESIAN; // Polar coordinates
            unsafe
            {
                tempEffect.periodic.direction.dir[1] = 1; // Force comes from south
            }

            if (_thrustmasterfix)
            {
                tempEffect.periodic.period = 50; // 1000 ms
            }
            else
            {
                tempEffect.periodic.period = 5; // 1000 ms
            }
            tempEffect.periodic.magnitude = (short)(_sineBase * strength); // 20000/32767 strength
            tempEffect.periodic.length = SDL.SDL_HAPTIC_INFINITY; // 5 seconds long
            tempEffect.periodic.attack_length = 100; // Takes 1 second to get max strength
            tempEffect.periodic.fade_length = 100; // Takes 1 second to fade away

            SDL.SDL_HapticUpdateEffect(_haptic, _effectSineId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectSineId, 1);
        }

        public void TriggerSineEffect(float strength)
        {
            // TODO: IMPLEMENT!
            SDL.SDL_HapticStopEffect(_haptic, _effectSineId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_SINE;
            tempEffect.periodic.direction.type = SDL.SDL_HAPTIC_CARTESIAN; // Polar coordinates
            unsafe
            {
                tempEffect.periodic.direction.dir[1] = 1; // Force comes from south
            }

            if (_thrustmasterfix)
            {
                tempEffect.periodic.period = 50; // 1000 ms
            }
            else
            {
                tempEffect.periodic.period = 5; // 1000 ms
            }
            tempEffect.periodic.magnitude = (short)(_sineBase * strength); // 20000/32767 strength
            tempEffect.periodic.length = 200; // 5 seconds long
            tempEffect.periodic.attack_length = 100; // Takes 1 second to get max strength
            tempEffect.periodic.fade_length = 100; // Takes 1 second to fade away

            SDL.SDL_HapticUpdateEffect(_haptic, _effectSineId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectSineId, 1);
        }

        public void TriggerFrictionEffect(float strength)
        {
            // TODO: IMPLEMENT!
            //SDL.SDL_HapticStopEffect(_haptic, _effectFrictionId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_FRICTION;
            tempEffect.condition.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
            tempEffect.condition.delay = 0;
            tempEffect.condition.length = 5000;
            unsafe
            {
                tempEffect.condition.direction.dir[0] = 1;
                tempEffect.condition.direction.dir[1] = 1;
                tempEffect.condition.direction.dir[2] = 0;
                tempEffect.condition.left_sat[0] = 0xFFFF;
                tempEffect.condition.right_sat[0] = 0xFFFF;
                tempEffect.condition.left_sat[1] = 0xFFFF;
                tempEffect.condition.right_sat[1] = 0xFFFF;
                tempEffect.condition.left_coeff[0] = (short)(strength * _frictionBase);
                tempEffect.condition.left_coeff[1] = (short)(strength * _frictionBase);
                tempEffect.condition.right_coeff[0] = (short)(strength * _frictionBase);
                tempEffect.condition.right_coeff[1] = (short)(strength * _frictionBase);
                tempEffect.condition.center[0] = 0x1000;
                tempEffect.condition.center[1] = 0x1000;
            }

            SDL.SDL_HapticUpdateEffect(_haptic, _effectFrictionId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectFrictionId, 1);
        }

        public void TriggerFrictionEffectInfinite(float strength)
        {
            // TODO: IMPLEMENT!
            //SDL.SDL_HapticStopEffect(_haptic, _effectFrictionId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_FRICTION;
            tempEffect.condition.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
            tempEffect.condition.delay = 0;
            tempEffect.condition.length = SDL.SDL_HAPTIC_INFINITY;
            unsafe
            {
                tempEffect.condition.direction.dir[0] = 1;
                tempEffect.condition.direction.dir[1] = 1;
                tempEffect.condition.direction.dir[2] = 0;
                tempEffect.condition.left_sat[0] = 0xFFFF;
                tempEffect.condition.right_sat[0] = 0xFFFF;
                tempEffect.condition.left_sat[1] = 0xFFFF;
                tempEffect.condition.right_sat[1] = 0xFFFF;
                tempEffect.condition.left_coeff[0] = (short)(strength * _frictionBase);
                tempEffect.condition.left_coeff[1] = (short)(strength * _frictionBase);
                tempEffect.condition.right_coeff[0] = (short)(strength * _frictionBase);
                tempEffect.condition.right_coeff[1] = (short)(strength * _frictionBase);
                tempEffect.condition.center[0] = 0x1000;
                tempEffect.condition.center[1] = 0x1000;
            }

            SDL.SDL_HapticUpdateEffect(_haptic, _effectFrictionId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectFrictionId, 1);
        }

        public void TriggerSpringEffect(float strength)
        {
            // TODO: IMPLEMENT!
            SDL.SDL_HapticStopEffect(_haptic, _effectSpringId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_SPRING;
            tempEffect.condition.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
            tempEffect.condition.delay = 0;
            tempEffect.condition.length = 5000;
            unsafe
            {
                tempEffect.condition.direction.dir[0] = 1;
                tempEffect.condition.direction.dir[1] = 1;
                tempEffect.condition.direction.dir[2] = 0;
                tempEffect.condition.left_sat[0] = 0;
                tempEffect.condition.right_sat[0] = 0;
                tempEffect.condition.left_coeff[0] = (short)(strength * _springBase);
                tempEffect.condition.right_coeff[0] = (short)(strength * _springBase);
                tempEffect.condition.center[0] = 0;
                tempEffect.condition.deadband[1] = 0;
                tempEffect.condition.left_sat[1] = 0;
                tempEffect.condition.right_sat[1] = 0;
                tempEffect.condition.left_coeff[1] = (short)(strength * _springBase);
                tempEffect.condition.right_coeff[1] = (short)(strength * _springBase);
                tempEffect.condition.center[1] = 0;
                tempEffect.condition.deadband[1] = 0;
            }

            SDL.SDL_HapticUpdateEffect(_haptic, _effectSpringId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectSpringId, 1);
        }

        public void TriggerSpringEffectInfinite(float strength)
        {
            // TODO: IMPLEMENT!
            //SDL.SDL_HapticStopEffect(_haptic, _effectSpringId);
            SDL.SDL_HapticEffect tempEffect = new SDL.SDL_HapticEffect();

            tempEffect.type = SDL.SDL_HAPTIC_SPRING;
            tempEffect.condition.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
            tempEffect.condition.delay = 0;
            tempEffect.condition.length = SDL.SDL_HAPTIC_INFINITY;
            unsafe
            {
                tempEffect.condition.interval = 100;
                tempEffect.condition.direction.dir[0] = 1;
                tempEffect.condition.direction.dir[1] = 1;
                tempEffect.condition.direction.dir[2] = 0;
                tempEffect.condition.left_sat[0] = 0;
                tempEffect.condition.right_sat[0] = 0;
                tempEffect.condition.left_coeff[0] = (short)(strength * _springBase);
                tempEffect.condition.right_coeff[0] = (short)(strength * _springBase);
                tempEffect.condition.center[0] = 0;
                tempEffect.condition.deadband[1] = 0;
                tempEffect.condition.left_sat[1] = 0;
                tempEffect.condition.right_sat[1] = 0;
                tempEffect.condition.left_coeff[1] = (short)(strength * _springBase);
                tempEffect.condition.right_coeff[1] = (short)(strength * _springBase);
                tempEffect.condition.center[1] = 0;
                tempEffect.condition.deadband[1] = 0;
            }

            SDL.SDL_HapticUpdateEffect(_haptic, _effectSpringId, ref tempEffect);

            SDL.SDL_HapticRunEffect(_haptic, _effectSpringId, 1);
        }

        public string InitializeHaptic(bool thrustmasterfix, string hapticName, Int16 constantBase = 2421, Int16 sineBase = 1022, Int16 frictionBase = 2421, Int16 springBase = 1000, int autocenter = 0)
        {
            _sineBase = sineBase;
            _frictionBase = frictionBase;
            _springBase = springBase;
            _constantBase = constantBase;
            _thrustmasterfix = thrustmasterfix;
            if (SDL.SDL_Init(SDL.SDL_INIT_HAPTIC | SDL.SDL_INIT_JOYSTICK) != 0)
            {
                return "Unable to initialize SDL, disabling Force Feedback!";
            }
            int numHaptics = SDL.SDL_NumHaptics();
            bool hasHaptic = false;
            int hapticId = 0;

            for (int i = 0; i < numHaptics; i++)
            {
                var val = SDL.SDL_HapticName(i);
                if (val == hapticName)
                {
                    hasHaptic = true;
                    hapticId = i;
                }
            }

            if (hasHaptic)
            {
                // Put i from joystickName here!
                _haptic = SDL.SDL_HapticOpen(hapticId);

                //SDL.SDL_HapticSetGain(_haptic, 100);

                SDL.SDL_HapticSetAutocenter(_haptic, autocenter);

                // left pull
                var tempEffect = new SDL.SDL_HapticEffect();

                tempEffect.type = SDL.SDL_HAPTIC_CONSTANT;
                tempEffect.constant.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
                unsafe
                {
                    tempEffect.constant.direction.dir[0] = -1;
                }
                tempEffect.constant.length = 30;
                tempEffect.constant.delay = 0;
                tempEffect.constant.level = 9999;

                // Upload the effect
                _effectLeftId = SDL.SDL_HapticNewEffect(_haptic, ref tempEffect);

                // Right pull

                var tempEffect2 = new SDL.SDL_HapticEffect();
                tempEffect2.type = SDL.SDL_HAPTIC_CONSTANT;
                tempEffect2.constant.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
                unsafe
                {
                    tempEffect2.constant.direction.dir[0] = 1;
                }

                tempEffect2.constant.length = 30;
                tempEffect2.constant.delay = 0;
                tempEffect2.constant.level = 9999;
                _effectRightId = SDL.SDL_HapticNewEffect(_haptic, ref tempEffect2);

                // Friction ????
                var tempEffect3 = new SDL.SDL_HapticEffect();
                tempEffect3.type = SDL.SDL_HAPTIC_FRICTION;
                tempEffect3.constant.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
                tempEffect3.condition.delay = 0;
                tempEffect3.condition.length = 5000;
                _effectFrictionId = SDL.SDL_HapticNewEffect(_haptic, ref tempEffect3);

                // Sine ????
                var tempEffect4 = new SDL.SDL_HapticEffect();
                tempEffect4.type = SDL.SDL_HAPTIC_SINE;
                tempEffect4.constant.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
                _effectSineId = SDL.SDL_HapticNewEffect(_haptic, ref tempEffect4);

                // Spring ???
                var tempEffect5 = new SDL.SDL_HapticEffect();
                tempEffect5.type = SDL.SDL_HAPTIC_SPRING;
                tempEffect5.condition.direction.type = SDL.SDL_HAPTIC_CARTESIAN;
                tempEffect5.condition.delay = 0;
                tempEffect5.condition.length = 5000;
                _effectSpringId = SDL.SDL_HapticNewEffect(_haptic, ref tempEffect5);

                if (_effectFrictionId == -1)
                {
                    return "Unable to create Friction effect, disabling Force Feedback!";
                }
                if (_effectLeftId == -1)
                {
                    return "Unable to create Constant Force effect, disabling Force Feedback!";
                }
                if (_effectRightId == -1)
                {
                    return "Unable to create Constant Force effect, disabling Force Feedback!";
                }
                if (_effectSineId == -1)
                {
                    return "Unable to create Sine effect, disabling Force Feedback!";
                }
                if (_effectSpringId == -1)
                {
                    return "Unable to create Spring effect, disabling Force Feedback!";
                }

                return string.Empty;
            }
            return "Cannot find your haptic device, is wheel connected?";
        }
    }
}
