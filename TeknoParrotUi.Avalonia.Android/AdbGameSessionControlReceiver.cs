using System;
using Android.App;
using Android.Content;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Gives Android's privileged shell a non-visual way to stop a test session.
/// The DUMP permission is signature/privileged, so ordinary applications cannot
/// use this exported receiver to interfere with a player's active game.
/// </summary>
[BroadcastReceiver(
    Name = ReceiverClass,
    Enabled = true,
    Exported = true,
    Permission = "android.permission.DUMP")]
[IntentFilter([StopAction])]
public sealed class AdbGameSessionControlReceiver : BroadcastReceiver
{
    public const string ReceiverClass =
        "com.teknoparrot.session.AdbGameSessionControlReceiver";
    public const string StopAction =
        "com.teknoparrot.ui.action.ADB_STOP_GAME_SESSION";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null ||
            !string.Equals(intent?.Action, StopAction, StringComparison.Ordinal))
        {
            return;
        }

        var stopIntent = new Intent(context, typeof(GameSessionService));
        stopIntent.SetAction(GameSessionService.StopAction);
        context.StartService(stopIntent);
    }
}
