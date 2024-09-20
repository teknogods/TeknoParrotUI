using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;

namespace TeknoParrotUi
{
    // Only collecting minimal data, in line with GDPR requirements.
    public static class Analytics
    {
        private static bool _isRunning = false;
        private static async Task<string> HttpGet(string url)
        {
            string result = "";
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                using (HttpClient client = new HttpClient((HttpMessageHandler)handler))
                {
                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        result = await response.Content.ReadAsStringAsync();
                    }
                }
            }

            return result;
        }
        public static async Task SendLaunchData(string gameName, EmulatorType emulationId)
        {
            try
            {
                if (gameName.Length >= 32)
                    gameName = gameName.Substring(0, 30);
                _isRunning = true;
                var myGuid =
                    await HttpGet(
                            $"https://teknoparrot.com/Home/SimpleAnonData?emulatorModule={(int)emulationId}&gameName={gameName}")
                        .ConfigureAwait(true);
                for (int i = 0; i < 300; i++)
                {
                    Thread.Sleep(1000);
                    if (!_isRunning)
                        break;
                }

                // No need to check for result.
                string resulting = "";
                if (_isRunning)
                    resulting = await HttpGet($"https://teknoparrot.com/Home/SimpleAnonEnd?generatedGuid={myGuid}")
                        .ConfigureAwait(true);
                _isRunning = false;
            }
            catch (Exception e)
            {
                _isRunning = false;
            }
        }

        public static void DisableSending()
        {
            // Just disable, even if it has been ran already who cares.
            _isRunning = false;
        }
    }
}
