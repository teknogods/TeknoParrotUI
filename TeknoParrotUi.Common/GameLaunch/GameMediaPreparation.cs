using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.GameLaunch
{
    public static class GameMediaPreparation
    {
        public static Task PrepareOnlineAsync(string gameId, Action<string> progress, CancellationToken cancellation)
            => TeknoCPSLauncher.PrepareMediaAsync(gameId, progress, cancellation);
    }
}
