using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace TeknoParrotUi.Common.InputListening.Plugins
{
    public class InputPluginManager
    {
        private List<IInputPlugin> _loadedPlugins = new List<IInputPlugin>();
        private Dictionary<string, Assembly> _pluginAssemblies = new Dictionary<string, Assembly>();

        public IEnumerable<IInputPlugin> GetActivePlugins()
        {
            return _loadedPlugins.Where(plugin => plugin.IsActive);
        }

        public void DiscoverPlugins()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Input");
            if (!Directory.Exists(pluginsDir))
            {
                Directory.CreateDirectory(pluginsDir);
                return; // No plugins yet
            }

            foreach (string file in Directory.GetFiles(pluginsDir, "*.dll"))
            {
                try
                {
                    Debug.WriteLine($"Loading plugin: {file}");
                    Assembly assembly = Assembly.LoadFrom(file);
                    _pluginAssemblies[Path.GetFileNameWithoutExtension(file)] = assembly;

                    // Find types implementing IInputPlugin
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (typeof(IInputPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            IInputPlugin plugin = (IInputPlugin)Activator.CreateInstance(type);
                            _loadedPlugins.Add(plugin);
                            Debug.WriteLine($"Loaded plugin: {plugin.Name} {plugin.Version}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading plugin {file}: {ex.Message}");
                }
            }
        }

        public List<IInputPlugin> GetPlugins() => _loadedPlugins;

        public void InitializeAll(GameProfile gameProfile)
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Initialize(gameProfile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error initializing plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        public void StartListeningAll(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.StartListening(joystickButtons, gameProfile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error starting plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        public void StopListeningAll()
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.StopListening();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        public void WndProcForAll(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in WndProc for plugin {plugin.Name}: {ex.Message}");
                }
            }
        }
    }
}