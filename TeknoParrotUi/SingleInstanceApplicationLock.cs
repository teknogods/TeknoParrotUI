using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;

namespace TeknoParrotUi
{
    public static class SingleApplicationDetector
    {
        public static bool IsRunning()
        {
            string guid = "TeknoParrot";
            var semaphoreName = @"Global\" + guid;
            try
            {
                __semaphore = Semaphore.OpenExisting(semaphoreName, SemaphoreRights.Synchronize);

                Close();
                return true;
            }
            catch (Exception ex)
            {
                __semaphore = new Semaphore(0, 1, semaphoreName);
                return false;
            }
        }

        public static void Close()
        {
            if (__semaphore != null)
            {
                __semaphore.Close();
                __semaphore = null;
            }
        }

        private static Semaphore __semaphore;
    }
}