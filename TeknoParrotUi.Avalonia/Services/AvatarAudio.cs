using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

public static class AvatarAudio
{
    public static Func<byte[], Task>? PlatformPlay { get; set; }
    public static Action? PlatformStop { get; set; }
    private static Process? _player;
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    private static extern bool PlaySound(byte[]? sound, IntPtr module, uint flags);
    public static async Task PlayAsync(byte[] wav)
    {
        Stop();
        if (PlatformPlay != null) { await PlatformPlay(wav); return; }
        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() => PlaySound(wav, IntPtr.Zero, 0x0004 | 0x0002));
            return;
        }
        var player = new Process { StartInfo = new ProcessStartInfo("aplay", "-q -")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true } };
        _player = player;
        try
        {
            player.Start();
            await player.StandardInput.BaseStream.WriteAsync(wav);
            player.StandardInput.Close();
            await player.WaitForExitAsync();
        }
        finally { if (ReferenceEquals(_player, player)) _player = null; player.Dispose(); }
    }
    public static void Stop()
    {
        PlatformStop?.Invoke();
        if (OperatingSystem.IsWindows()) PlaySound(null, IntPtr.Zero, 0);
        try { if (_player is { HasExited: false }) _player.Kill(); } catch (InvalidOperationException) { }
    }
}
