using System;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;

namespace TeknoParrotUi.Avalonia.Android;

internal static class AndroidAvatarAudio
{
    private static MediaPlayer? _player;
    private static string? _path;
    private static TaskCompletionSource? _completion;
    public static Task PlayAsync(Context context, byte[] wav)
    {
        Stop();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        _path = Path.Combine(context.CacheDir!.AbsolutePath, "tpui-avatar-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            File.WriteAllBytes(_path, wav);
            var player = _player = new MediaPlayer();
            player.Completion += (_, _) => { if (ReferenceEquals(_player, player)) Stop(); };
            player.Error += (_, args) => { args.Handled = true; if (ReferenceEquals(_player, player)) Stop(); };
            player.SetDataSource(_path);
            player.Prepare();
            player.Start();
        }
        catch (Exception error) { completion.TrySetException(error); Stop(); }
        return completion.Task;
    }
    public static void Stop()
    {
        var player = _player; _player = null;
        var path = _path; _path = null;
        var completion = _completion; _completion = null;
        try { player?.Stop(); } catch (Java.Lang.IllegalStateException) { }
        player?.Release(); player?.Dispose();
        if (path != null) { try { File.Delete(path); } catch (IOException) { } }
        completion?.TrySetResult();
    }
}
