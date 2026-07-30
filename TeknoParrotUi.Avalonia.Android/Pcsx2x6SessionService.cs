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

/// <summary>
/// Durable owner for a PCSX2X6/ARMSX2 arcade session. The service survives
/// Avalonia Activity recreation, authenticates callbacks with a per-run token,
/// and keeps an explicit Stop action available in Android's notification area.
/// </summary>
[Service(
    Name = ServiceClass,
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public sealed class Pcsx2x6SessionService : Service
{
    public const string ServiceClass =
        "com.teknoparrot.session.Pcsx2x6SessionService";
    public const string StartAction =
        "com.teknoparrot.ui.action.START_PCSX2X6_SESSION";
    public const string StopAction =
        "com.teknoparrot.ui.action.STOP_PCSX2X6_SESSION";
    public const string ProfileNameExtra =
        "com.teknoparrot.ui.extra.PCSX2X6_PROFILE_NAME";
    public const string ManifestPathExtra =
        "com.teknoparrot.ui.extra.PCSX2X6_MANIFEST_PATH";
    public const string SessionTokenExtra =
        "com.teknoparrot.ui.extra.PCSX2X6_SESSION_TOKEN";

    private const string ArmsPackage = "com.teknogods.tekno2x6";
    private const string ArmsActivity = "com.armsx2.Main";
    private const string ArmsControlReceiver =
        "com.armsx2.TeknoParrotSessionControlReceiver";
    private const string LaunchAction =
        "com.teknoparrot.pcsx2x6.action.LAUNCH_GAME";
    private const string QueryAction =
        "com.teknoparrot.pcsx2x6.action.QUERY_SESSION";
    private const string RemoteStopAction =
        "com.teknoparrot.pcsx2x6.action.STOP_GAME";
    private const string StatusAction =
        "com.teknoparrot.pcsx2x6.action.SESSION_STATUS";
    private const string RemoteGamePathExtra =
        "com.teknoparrot.pcsx2x6.extra.GAME_PATH";
    private const string RemoteProfileNameExtra =
        "com.teknoparrot.pcsx2x6.extra.PROFILE_NAME";
    private const string RemoteInputPagePathExtra =
        "com.teknoparrot.pcsx2x6.extra.INPUT_PAGE_PATH";
    private const string RemoteCallbackPackageExtra =
        "com.teknoparrot.pcsx2x6.extra.CALLBACK_PACKAGE";
    private const string RemoteSessionTokenExtra =
        "com.teknoparrot.pcsx2x6.extra.SESSION_TOKEN";
    private const string RemoteSessionStatusExtra =
        "com.teknoparrot.pcsx2x6.extra.SESSION_STATUS";

    private const string PreferencesName = "teknoparrot-pcsx2x6-session";
    private const string RecordKey = "active-session-v1";
    private const string NotificationChannelId =
        "teknoparrot_pcsx2x6_session";
    private const int NotificationId = 0x5836;
    private static readonly object StatusSync = new();
    private static string _status = "state=idle";

    private SessionStatusReceiver? _statusReceiver;
    private SessionRecord? _record;
    private CancellationTokenSource? _healthStop;
    private Task? _healthTask;
    private long _lastResponseAt;
    private bool _foregroundStarted;

    internal static event Action<string>? StatusChanged;

    internal static string CurrentStatus
    {
        get
        {
            lock (StatusSync)
                return _status;
        }
    }

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
        _statusReceiver = new SessionStatusReceiver(this);
        var filter = new IntentFilter(StatusAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RegisterReceiver(_statusReceiver, filter, ReceiverFlags.Exported);
        else
#pragma warning disable CA1422
            RegisterReceiver(_statusReceiver, filter);
#pragma warning restore CA1422
    }

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId)
    {
        if (string.Equals(intent?.Action, StopAction, StringComparison.Ordinal))
        {
            RequestStop();
            return StartCommandResult.Sticky;
        }

        try
        {
            StartForegroundSession("Preparing PCSX2X6 arcade session");
            var saved = LoadRecord();
            if (string.Equals(intent?.Action, StartAction, StringComparison.Ordinal))
            {
                var requested = CreateRecord(intent!);
                if (saved != null &&
                    !string.Equals(saved.Token, requested.Token, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{saved.ProfileName} already owns the PCSX2X6 session.");
                }

                _record = saved ?? requested;
                SaveRecord(_record);
                PublishStatus("state=starting;detail=opening PCSX2X6");
                LaunchCompanion(_record);
                StartHealthMonitor(_record);
            }
            else
            {
                _record = saved;
                if (_record == null)
                {
                    PublishStatus("state=idle");
                    StopForegroundSession();
                    StopSelf(startId);
                    return StartCommandResult.NotSticky;
                }

                PublishStatus("state=restoring;detail=checking PCSX2X6 session");
                QueryCompanion(_record);
                StartHealthMonitor(_record);
            }

            return StartCommandResult.Sticky;
        }
        catch (Exception error)
        {
            PublishStatus("state=fault;error=" + Sanitize(error.Message));
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
        if (_statusReceiver != null)
        {
            try
            {
                UnregisterReceiver(_statusReceiver);
            }
            catch (Java.Lang.IllegalArgumentException)
            {
                // The process can be torn down between registration and cleanup.
            }
            _statusReceiver.Dispose();
            _statusReceiver = null;
        }
        base.OnDestroy();
    }

    internal static Intent CreateStartIntent(
        Context context,
        string profileName,
        string manifestPath)
    {
        var intent = new Intent(context, typeof(Pcsx2x6SessionService));
        intent.SetAction(StartAction);
        intent.PutExtra(ProfileNameExtra, profileName);
        intent.PutExtra(ManifestPathExtra, manifestPath);
        intent.PutExtra(
            SessionTokenExtra,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return intent;
    }

    internal static string? TryGetActiveProfileName(Context context)
    {
        try
        {
            var json = context.GetSharedPreferences(
                    PreferencesName,
                    FileCreationMode.Private)
                ?.GetString(RecordKey, null);
            var record = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<SessionRecord>(json);
            return IsValidRecord(record) ? record!.ProfileName : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? TryRestoreActiveProfileName(Context context)
    {
        var profileName = TryGetActiveProfileName(context);
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        var status = CurrentStatus;
        if (string.Equals(status, "state=idle", StringComparison.Ordinal) ||
            status.StartsWith("state=fault", StringComparison.Ordinal))
        {
            try
            {
                var applicationContext = context.ApplicationContext ?? context;
                applicationContext.StartForegroundService(
                    new Intent(applicationContext, typeof(Pcsx2x6SessionService)));
            }
            catch (Exception error)
            {
                PublishStaticStatus(
                    "state=fault;error=" + Sanitize(error.Message));
            }
        }
        return profileName;
    }

    private static SessionRecord CreateRecord(Intent intent)
    {
        var profileName =
            intent.GetStringExtra(ProfileNameExtra)?.Trim() ?? string.Empty;
        var manifestPath =
            intent.GetStringExtra(ManifestPathExtra)?.Trim() ?? string.Empty;
        var token =
            intent.GetStringExtra(SessionTokenExtra)?.Trim() ?? string.Empty;
        var record = new SessionRecord(profileName, manifestPath, token);
        if (!IsValidRecord(record))
            throw new InvalidDataException(
                "The PCSX2X6 profile, manifest path, or session token is invalid.");
        return record;
    }

    private static bool IsValidRecord(SessionRecord? record)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.ProfileName) ||
            record.ProfileName.Length > 256 ||
            string.IsNullOrWhiteSpace(record.ManifestPath) ||
            record.ManifestPath.Length > 1024 ||
            !record.ManifestPath.StartsWith(
                "/storage/emulated/0/Android/data/com.teknogods.tekno2x6/files/TeknoParrot/games/",
                StringComparison.Ordinal) ||
            !record.ManifestPath.EndsWith(".acgame", StringComparison.OrdinalIgnoreCase) ||
            record.Token.Length is < 32 or > 128)
        {
            return false;
        }

        return record.Token.AsSpan().IndexOfAnyExcept(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_") < 0;
    }

    private void LaunchCompanion(SessionRecord record)
    {
        _ = PackageManager?.GetPackageInfo(ArmsPackage, PackageInfoFlags.Activities)
            ?? throw new InvalidOperationException(
                "PCSX2X6 ARM is not installed.");

        var launch = new Intent(LaunchAction);
        launch.SetComponent(new ComponentName(ArmsPackage, ArmsActivity));
        launch.PutExtra(RemoteGamePathExtra, record.ManifestPath);
        launch.PutExtra(RemoteProfileNameExtra, record.ProfileName);
        launch.PutExtra(
            RemoteInputPagePathExtra,
            "/storage/emulated/0/Android/data/com.teknogods.tekno2x6/files/" +
            "TeknoParrot/bridge/TeknoParrot_JvsState.page");
        launch.PutExtra(RemoteCallbackPackageExtra, PackageName);
        launch.PutExtra(RemoteSessionTokenExtra, record.Token);
        launch.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        StartActivity(launch);
    }

    private void QueryCompanion(SessionRecord record) =>
        SendControlBroadcast(QueryAction, record);

    private void RequestStop()
    {
        var record = _record ?? LoadRecord();
        if (record == null)
        {
            PublishStatus("state=stopped");
            StopForegroundSession();
            StopSelf();
            return;
        }

        _record = record;
        PublishStatus("state=stopping;detail=requesting PCSX2X6 shutdown");
        SendControlBroadcast(RemoteStopAction, record);
    }

    private void SendControlBroadcast(string action, SessionRecord record)
    {
        var control = new Intent(action);
        control.SetComponent(new ComponentName(ArmsPackage, ArmsControlReceiver));
        control.PutExtra(RemoteCallbackPackageExtra, PackageName);
        control.PutExtra(RemoteSessionTokenExtra, record.Token);
        SendBroadcast(control);
    }

    private void HandleRemoteStatus(Intent intent)
    {
        var record = _record ?? LoadRecord();
        if (record == null)
            return;

        var token = intent.GetStringExtra(RemoteSessionTokenExtra);
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(record.Token),
                System.Text.Encoding.ASCII.GetBytes(token ?? string.Empty)))
        {
            return;
        }

        Interlocked.Exchange(ref _lastResponseAt, SystemClock.ElapsedRealtime());
        var status = intent.GetStringExtra(RemoteSessionStatusExtra) ?? string.Empty;
        switch (status)
        {
            case "accepted":
                PublishStatus("state=accepted;detail=PCSX2X6 accepted the game");
                StartForegroundSession(record.ProfileName + " is starting");
                break;
            case "running":
                PublishStatus("state=running;detail=PCSX2X6 game is active");
                StartForegroundSession(record.ProfileName + " is running");
                break;
            case "stopping":
                PublishStatus("state=stopping;detail=PCSX2X6 is stopping");
                StartForegroundSession(record.ProfileName + " is stopping");
                break;
            case "stopped":
                PublishStatus("state=stopped");
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
        if (_healthTask is { IsCompleted: false })
            return;

        _healthStop?.Dispose();
        _healthStop = new CancellationTokenSource();
        Interlocked.Exchange(ref _lastResponseAt, SystemClock.ElapsedRealtime());
        var cancellationToken = _healthStop.Token;
        _healthTask = Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)
                        .ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    try
                    {
                        QueryCompanion(record);
                    }
                    catch (Exception error)
                    {
                        global::Android.Util.Log.Warn(
                            "TeknoParrotPCSX2X6",
                            "Session query failed: " + Sanitize(error.Message));
                    }

                    var silentFor =
                        SystemClock.ElapsedRealtime() -
                        Interlocked.Read(ref _lastResponseAt);
                    if (silentFor <= 30_000)
                        continue;

                    PublishStatus(
                        "state=fault;error=PCSX2X6 is no longer responding");
                    ClearRecord();
                    _record = null;
                    StopForegroundSession();
                    StopSelf();
                    return;
                }
            }
            catch (System.OperationCanceledException)
            {
                // Normal terminal-session cleanup.
            }
        }, cancellationToken);
    }

    private void StopHealthMonitor()
    {
        var stop = Interlocked.Exchange(ref _healthStop, null);
        stop?.Cancel();
        stop?.Dispose();
        _healthTask = null;
    }

    private SessionRecord? LoadRecord()
    {
        try
        {
            var json = GetSharedPreferences(PreferencesName, FileCreationMode.Private)
                ?.GetString(RecordKey, null);
            var record = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<SessionRecord>(json);
            return IsValidRecord(record) ? record : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SaveRecord(SessionRecord record)
    {
        GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.Edit()
            ?.PutString(RecordKey, JsonSerializer.Serialize(record))
            ?.Commit();
    }

    private void ClearRecord()
    {
        GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.Edit()
            ?.Remove(RecordKey)
            ?.Commit();
    }

    private void CreateNotificationChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            NotificationChannelId,
            "PCSX2X6 arcade session",
            NotificationImportance.Low)
        {
            Description =
                "Keeps TeknoParrot connected to the running PCSX2X6 arcade game."
        });
    }

    private void StartForegroundSession(string detail)
    {
        var notification = BuildNotification(detail);
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotificationId, notification);
        _foregroundStarted = true;
    }

    private Notification BuildNotification(string detail)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPending = PendingIntent.GetActivity(
            this,
            0,
            openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var stopIntent = new Intent(this, typeof(Pcsx2x6SessionService));
        stopIntent.SetAction(StopAction);
        var stopPending = PendingIntent.GetService(
            this,
            1,
            stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
            ?? throw new InvalidOperationException(
                "Could not create the PCSX2X6 Stop action.");
        var stopAction = new Notification.Action.Builder(
                global::Android.Graphics.Drawables.Icon.CreateWithResource(
                    this,
                    global::Android.Resource.Drawable.IcMenuCloseClearCancel),
                "Stop",
                stopPending)
            .Build();

        return new Notification.Builder(this, NotificationChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("TeknoParrot PCSX2X6 session")
            .SetContentText(detail)
            .SetContentIntent(openPending)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryService)
            .AddAction(stopAction)
            .Build();
    }

    private void StopForegroundSession()
    {
        if (!_foregroundStarted)
            return;
        _foregroundStarted = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(24))
            StopForeground(StopForegroundFlags.Remove);
        else
#pragma warning disable CA1422
            StopForeground(true);
#pragma warning restore CA1422
    }

    private void PublishStatus(string status) => PublishStaticStatus(status);

    private static void PublishStaticStatus(string status)
    {
        Action<string>? changed;
        lock (StatusSync)
        {
            _status = status;
            changed = StatusChanged;
        }
        changed?.Invoke(status);
    }

    private static string Sanitize(string? value) =>
        (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    private sealed class SessionStatusReceiver(
        Pcsx2x6SessionService owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent != null &&
                string.Equals(intent.Action, StatusAction, StringComparison.Ordinal))
            {
                owner.HandleRemoteStatus(intent);
            }
        }
    }

    private sealed record SessionRecord(
        string ProfileName,
        string ManifestPath,
        string Token);
}
