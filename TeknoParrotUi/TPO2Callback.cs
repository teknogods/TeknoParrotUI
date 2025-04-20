using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common;

public class TPO2Callback
{
    public event Action GameProcessExited;
    private bool isLaunched = false;
    private IGameProcess _gameProcess;
    private string uniqueRoomName;

    public TPO2Callback()
    {
        // Create the appropriate game process handler for the current platform
        _gameProcess = GameProcessFactory.CreateGameProcess();
    }

    public void showMessage(string msg)
    {
        _ = MessageBoxHelper.InfoOK(msg);
    }

    public void startGame(string uniqueRoomName, string realRoomName, string gameId, string playerId, string playerName, string playerCount)
    {
        if (_gameProcess.IsRunning)
        {
            _ = MessageBoxHelper.InfoOK("Game is already running.");
            return;
        }
        Console.WriteLine($"Launching Game now ok: {gameId}");
        this.uniqueRoomName = uniqueRoomName; // Store unique room name for later use

        var profileName = gameId + ".xml";

        // Set up environment variables and arguments in a platform-neutral way
        var environmentVars = new System.Collections.Generic.Dictionary<string, string>
            {
                { "TP_TPONLINE2", $"{uniqueRoomName}|{playerId}|{playerName}|{playerCount}" }
            };

        string exeName = GetExecutableName("TeknoParrotUi");
        string args = $"--profile={profileName} --tponline";

        // Start the game process
        _gameProcess.Start(exeName, args, environmentVars);

        // Register for the process exit event
        _gameProcess.ProcessExited += GameProcess_Exited;

        isLaunched = true;
    }

    private string GetExecutableName(string baseName)
    {
        // Add the proper extension based on the platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"{baseName}.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return baseName;
        }

        // Default case, just return the base name
        return baseName;
    }

    private void GameProcess_Exited(object sender, EventArgs e)
    {
        isLaunched = false;

        // Notify the UI
        OnGameProcessExited();
    }

    protected void OnGameProcessExited()
    {
        GameProcessExited?.Invoke();
    }
}

// Factory to create the appropriate game process handler for the current platform
public static class GameProcessFactory
{
    public static IGameProcess CreateGameProcess()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsGameProcess();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxGameProcess();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsGameProcess();
        }

        throw new PlatformNotSupportedException("Current platform is not supported.");
    }
}

// Interface for platform-specific game process handling
public interface IGameProcess
{
    bool IsRunning { get; }
    event EventHandler ProcessExited;
    void Start(string executablePath, string arguments, System.Collections.Generic.Dictionary<string, string> environmentVars);
    void Terminate();
}

// Windows implementation using System.Diagnostics.Process
public class WindowsGameProcess : IGameProcess
{
    private Process _process;

    public bool IsRunning => _process != null && !_process.HasExited;

    public event EventHandler ProcessExited;

    public void Start(string executablePath, string arguments, System.Collections.Generic.Dictionary<string, string> environmentVars)
    {
        var startInfo = new ProcessStartInfo(executablePath, arguments)
        {
            UseShellExecute = false
        };

        // Add environment variables
        foreach (var envVar in environmentVars)
        {
            startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
        }

        _process = Process.Start(startInfo);

        if (_process != null)
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => ProcessExited?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Terminate()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // Ignore errors during termination
            }
        }
    }
}

// Linux implementation
public class LinuxGameProcess : IGameProcess
{
    private Process _process;

    public bool IsRunning => _process != null && !_process.HasExited;

    public event EventHandler ProcessExited;

    public void Start(string executablePath, string arguments, System.Collections.Generic.Dictionary<string, string> environmentVars)
    {
        var startInfo = new ProcessStartInfo("/bin/bash", $"-c \"{executablePath} {arguments}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Add environment variables
        foreach (var envVar in environmentVars)
        {
            startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
        }

        _process = Process.Start(startInfo);

        if (_process != null)
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => ProcessExited?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Terminate()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // Ignore errors during termination
            }
        }
    }
}

// macOS implementation
public class MacOsGameProcess : IGameProcess
{
    private Process _process;

    public bool IsRunning => _process != null && !_process.HasExited;

    public event EventHandler ProcessExited;

    public void Start(string executablePath, string arguments, System.Collections.Generic.Dictionary<string, string> environmentVars)
    {
        // macOS might require permission to execute, so make it executable first
        Process.Start("chmod", $"+x {executablePath}");

        var startInfo = new ProcessStartInfo("open", $"-a {executablePath} --args {arguments}")
        {
            UseShellExecute = false
        };

        // Add environment variables
        foreach (var envVar in environmentVars)
        {
            startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
        }

        _process = Process.Start(startInfo);

        if (_process != null)
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => ProcessExited?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Terminate()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // Ignore errors during termination
            }
        }
    }
}