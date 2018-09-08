using System.Linq;

namespace TeknoParrotUi.Common
{
    public class Helper
    {
        /// <summary>
        /// Joystick information strings tend to have useless zeroes, let's remove them.
        /// </summary>
        /// <param name="value">String with zeroes.</param>
        /// <returns>Clean string.</returns>
        public static string ExtractWithoutZeroes(string value)
        {
            return value.Split("\0".ToCharArray()).FirstOrDefault();
        }
    }
}
