using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>Durable foreground owner for one authenticated RPCS3X6 session.</summary>
[Service(
    Name = ServiceClass,
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public sealed class Rpcs3x6SessionService : Service
{
    public const string ServiceClass = "com.teknoparrot.session.Rpcs3x6SessionService";
    public const string StartAction = "com.teknoparrot.ui.action.START_RPCS3X6_SESSION";
    public const string StopAction = "com.teknoparrot.ui.action.STOP_RPCS3X6_SESSION";
    public const string ProfileExtra = "com.teknoparrot.ui.extra.RPCS3X6_PROFILE";
    public const string GameIdExtra = "com.teknoparrot.ui.extra.RPCS3X6_GAME_ID";
    public const string GamePathExtra = "com.teknoparrot.ui.extra.RPCS3X6_GAME_PATH";
    public const string TokenExtra = "com.teknoparrot.ui.extra.RPCS3X6_TOKEN";

    private const string CompanionPackage = "com.teknogods.rpcs3x6";
    private const string CompanionActivity =
        "net.rpcs3.RPCS3Activity";
    private const string CompanionReceiver =
        "net.rpcs3.TeknoParrotSessionControlReceiver";
    private const string RemotePrefix = "com.teknoparrot.rpcs3x6";
    private const string LaunchAction = RemotePrefix + ".action.LAUNCH_GAME";
    private const string QueryAction = RemotePrefix + ".action.QUERY_SESSION";
    private const string RemoteStopAction = RemotePrefix + ".action.STOP_GAME";
    private const string StatusAction = RemotePrefix + ".action.SESSION_STATUS";
    private const string RemoteGamePath = RemotePrefix + ".extra.GAME_PATH";
    private const string RemoteGameId = RemotePrefix + ".extra.GAME_ID";
    private const string RemoteProfile = RemotePrefix + ".extra.PROFILE_NAME";
    private const string RemoteInputPage = RemotePrefix + ".extra.INPUT_PAGE_PATH";
    private const string RemoteCallback = RemotePrefix + ".extra.CALLBACK_PACKAGE";
    private const string RemoteToken = RemotePrefix + ".extra.SESSION_TOKEN";
    private const string RemoteStatus = RemotePrefix + ".extra.SESSION_STATUS";
    private const string Preferences = "teknoparrot-rpcs3x6-session";
    private const string RecordKey = "active-session-v1";
    private const string ChannelId = "teknoparrot_rpcs3x6_session";
    private const int NotificationId = 0xD011;

    private static readonly object StatusSync = new();
    private static string _status = "state=idle";
    private StatusReceiver? _receiver;
    private SessionRecord? _record;
    private CancellationTokenSource? _healthStop;
    private long _lastResponseAt;
    private bool _foreground;

    internal static event Action<string>? StatusChanged;
    internal static string CurrentStatus
    {
        get { lock (StatusSync) return _status; }
    }

    public override void OnCreate()
    {
        base.OnCreate();
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId, "RPCS3X6 arcade session", NotificationImportance.Low)
        {
            Description = "Keeps TeknoParrot connected to the running RPCS3X6 game."
        });
        _receiver = new StatusReceiver(this);
        var filter = new IntentFilter(StatusAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
        else
#pragma warning disable CA1422
            RegisterReceiver(_receiver, filter);
#pragma warning restore CA1422
    }

    public override StartCommandResult OnStartCommand(
        Intent? intent, StartCommandFlags flags, int startId)
    {
        if (string.Equals(intent?.Action, StopAction, StringComparison.Ordinal))
        {
            RequestStop();
            return StartCommandResult.Sticky;
        }
        try
        {
            StartForegroundSession("Preparing RPCS3X6 arcade session");
            var saved = LoadRecord();
            if (string.Equals(intent?.Action, StartAction, StringComparison.Ordinal))
            {
                var requested = CreateRecord(intent!);
                if (saved != null && saved.Token != requested.Token)
                    throw new InvalidOperationException(
                        $"{saved.ProfileName} already owns the RPCS3X6 session.");
                _record = saved ?? requested;
                SaveRecord(_record);
                Publish("state=starting;detail=opening RPCS3X6");
                Launch(_record);
            }
            else
            {
                _record = saved;
                if (_record == null)
                {
                    Publish("state=idle");
                    StopForegroundSession();
                    StopSelf(startId);
                    return StartCommandResult.NotSticky;
                }
                Publish("state=restoring;detail=checking RPCS3X6 session");
                SendControl(QueryAction, _record);
            }
            StartHealthMonitor(_record);
            return StartCommandResult.Sticky;
        }
        catch (Exception error)
        {
            Publish("state=fault;error=" + Sanitize(error.Message));
            ClearRecord();
            StopForegroundSession();
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        StopHealthMonitor();
        if (_receiver != null)
        {
            try { UnregisterReceiver(_receiver); }
            catch (Java.Lang.IllegalArgumentException) { }
            _receiver.Dispose();
            _receiver = null;
        }
        base.OnDestroy();
    }

    internal static Intent CreateStartIntent(
        Context context, string profile, string gameId, string gamePath)
    {
        var intent = new Intent(context, typeof(Rpcs3x6SessionService));
        intent.SetAction(StartAction);
        intent.PutExtra(ProfileExtra, profile);
        intent.PutExtra(GameIdExtra, gameId);
        intent.PutExtra(GamePathExtra, gamePath);
        intent.PutExtra(TokenExtra, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return intent;
    }

    internal static string? TryGetActiveProfileName(Context context)
    {
        try
        {
            var json = context.GetSharedPreferences(Preferences, FileCreationMode.Private)
                ?.GetString(RecordKey, null);
            var record = string.IsNullOrWhiteSpace(json)
                ? null : JsonSerializer.Deserialize<SessionRecord>(json);
            return IsValid(record) ? record!.ProfileName : null;
        }
        catch (JsonException) { return null; }
    }

    internal static string? TryRestoreActiveProfileName(Context context)
    {
        var profile = TryGetActiveProfileName(context);
        if (string.IsNullOrWhiteSpace(profile))
            return null;
        if (CurrentStatus == "state=idle" || CurrentStatus.StartsWith("state=fault"))
            (context.ApplicationContext ?? context).StartForegroundService(
                new Intent(context, typeof(Rpcs3x6SessionService)));
        return profile;
    }

    private static SessionRecord CreateRecord(Intent intent)
    {
        var record = new SessionRecord(
            intent.GetStringExtra(ProfileExtra)?.Trim() ?? "",
            intent.GetStringExtra(GameIdExtra)?.Trim() ?? "",
            intent.GetStringExtra(GamePathExtra)?.Trim() ?? "",
            intent.GetStringExtra(TokenExtra)?.Trim() ?? "");
        if (!IsValid(record))
            throw new InvalidDataException("The RPCS3X6 session envelope is invalid.");
        return record;
    }

    private static bool IsValid(SessionRecord? record) =>
        record != null &&
        record.ProfileName.Length is > 0 and <= 256 &&
        record.GameId.Length <= 128 &&
        record.GamePath.StartsWith(
            "/storage/emulated/0/Android/data/com.teknogods.rpcs3x6/files/TeknoParrot/arcade/",
            StringComparison.Ordinal) &&
        record.Token.Length is >= 32 and <= 128 &&
        record.Token.AsSpan().IndexOfAnyExcept(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_") < 0;

    private void Launch(SessionRecord record)
    {
        _ = PackageManager?.GetPackageInfo(CompanionPackage, PackageInfoFlags.Activities)
            ?? throw new InvalidOperationException("RPCS3X6 is not installed.");
        var intent = new Intent(LaunchAction);
        intent.SetComponent(new ComponentName(CompanionPackage, CompanionActivity));
        intent.PutExtra(RemoteGamePath, record.GamePath);
        intent.PutExtra(RemoteGameId, record.GameId);
        intent.PutExtra(RemoteProfile, record.ProfileName);
        intent.PutExtra(RemoteInputPage,
            "/storage/emulated/0/Android/data/com.teknogods.rpcs3x6/files/" +
            "TeknoParrot/bridge/TeknoParrot_JvsState.page");
        intent.PutExtra(RemoteCallback, PackageName);
        intent.PutExtra(RemoteToken, record.Token);
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        StartActivity(intent);
    }

    private void RequestStop()
    {
        var record = _record ?? LoadRecord();
        if (record == null)
        {
            Publish("state=stopped");
            StopForegroundSession();
            StopSelf();
            return;
        }
        _record = record;
        Publish("state=stopping;detail=requesting RPCS3X6 shutdown");
        SendControl(RemoteStopAction, record);
    }

    private void SendControl(string action, SessionRecord record)
    {
        var intent = new Intent(action);
        intent.SetComponent(new ComponentName(CompanionPackage, CompanionReceiver));
        intent.PutExtra(RemoteCallback, PackageName);
        intent.PutExtra(RemoteToken, record.Token);
        SendBroadcast(intent);
    }

    private void HandleStatus(Intent intent)
    {
        var record = _record ?? LoadRecord();
        if (record == null)
            return;
        var token = intent.GetStringExtra(RemoteToken) ?? "";
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(record.Token),
                System.Text.Encoding.ASCII.GetBytes(token)))
            return;
        Interlocked.Exchange(ref _lastResponseAt, SystemClock.ElapsedRealtime());
        switch (intent.GetStringExtra(RemoteStatus))
        {
            case "accepted":
                Publish("state=accepted;detail=RPCS3X6 accepted the game");
                StartForegroundSession(record.ProfileName + " is starting");
                break;
            case "running":
                Publish("state=running;detail=RPCS3X6 game is active");
                StartForegroundSession(record.ProfileName + " is running");
                break;
            case "stopping":
                Publish("state=stopping;detail=RPCS3X6 is stopping");
                break;
            case "stopped":
                Publish("state=stopped");
                StopHealthMonitor();
                ClearRecord();
                _record = null;
                StopForegroundSession();
                StopSelf();
                break;
            case "failed":
                Publish("state=fault;error=RPCS3X6 rejected or could not boot the game");
                StopHealthMonitor();
                ClearRecord();
                _record = null;
                StopForegroundSession();
                StopSelf();
                break;
        }
    }

    private void StartHealthMonitor(SessionRecord record)
    {
        StopHealthMonitor();
        _healthStop = new CancellationTokenSource();
        Interlocked.Exchange(ref _lastResponseAt, SystemClock.ElapsedRealtime());
        var cancellation = _healthStop.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellation)
                        .ConfigureAwait(false);
                    SendControl(QueryAction, record);
                    if (SystemClock.ElapsedRealtime() -
                        Interlocked.Read(ref _lastResponseAt) <= 30_000)
                        continue;
                    Publish("state=fault;error=RPCS3X6 is no longer responding");
                    ClearRecord();
                    _record = null;
                    StopForegroundSession();
                    StopSelf();
                    return;
                }
            }
            catch (System.OperationCanceledException) { }
            catch (Exception error)
            {
                global::Android.Util.Log.Warn("TeknoParrotRpcs3x6", Sanitize(error.Message));
            }
        }, cancellation);
    }

    private void StopHealthMonitor()
    {
        var stop = Interlocked.Exchange(ref _healthStop, null);
        stop?.Cancel();
        stop?.Dispose();
    }

    private SessionRecord? LoadRecord()
    {
        try
        {
            var json = GetSharedPreferences(Preferences, FileCreationMode.Private)
                ?.GetString(RecordKey, null);
            var record = string.IsNullOrWhiteSpace(json)
                ? null : JsonSerializer.Deserialize<SessionRecord>(json);
            return IsValid(record) ? record : null;
        }
        catch (JsonException) { return null; }
    }

    private void SaveRecord(SessionRecord record) =>
        GetSharedPreferences(Preferences, FileCreationMode.Private)?.Edit()
            ?.PutString(RecordKey, JsonSerializer.Serialize(record))?.Commit();

    private void ClearRecord() =>
        GetSharedPreferences(Preferences, FileCreationMode.Private)?.Edit()
            ?.Remove(RecordKey)?.Commit();

    private void StartForegroundSession(string detail)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        var openPending = PendingIntent.GetActivity(this, 0, openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var stopIntent = new Intent(this, typeof(Rpcs3x6SessionService));
        stopIntent.SetAction(StopAction);
        var stopPending = PendingIntent.GetService(this, 2, stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var stopAction = new Notification.Action.Builder(
            global::Android.Graphics.Drawables.Icon.CreateWithResource(
                this, global::Android.Resource.Drawable.IcMenuCloseClearCancel),
            "Stop", stopPending).Build();
        var notification = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("TeknoParrot RPCS3X6 session")
            .SetContentText(detail)
            .SetContentIntent(openPending)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryService)
            .AddAction(stopAction)
            .Build();
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotificationId, notification);
        _foreground = true;
    }

    private void StopForegroundSession()
    {
        if (!_foreground)
            return;
        _foreground = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(24))
            StopForeground(StopForegroundFlags.Remove);
        else
#pragma warning disable CA1422
            StopForeground(true);
#pragma warning restore CA1422
    }

    private static void Publish(string status)
    {
        Action<string>? changed;
        lock (StatusSync) { _status = status; changed = StatusChanged; }
        changed?.Invoke(status);
    }

    private static string Sanitize(string? value) =>
        (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed class StatusReceiver(Rpcs3x6SessionService owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == StatusAction)
                owner.HandleStatus(intent);
        }
    }

    private sealed record SessionRecord(
        string ProfileName, string GameId, string GamePath, string Token);
}
