using System.Collections.Generic;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputBinding : IInputBinding
    {
        // The key/button code that was pressed
        public int KeyCode { get; set; }

        // Human-readable name of this binding
        public string DisplayName { get; set; }

        // Plugin that created this binding
        public string PluginName { get; set; }

        public InputSourceType SourceType { get; set; }

        public InputBinding()
        {
            DisplayName = "None";
            PluginName = "";
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class InputBindingsData
    {
        public string GameId { get; set; }
        public string GameName { get; set; }
        public string PluginName { get; set; }
        public List<InputBindingInfo> Bindings { get; set; } = new List<InputBindingInfo>();
    }

    public class InputBindingInfo
    {
        public string ButtonName { get; set; }
        public string InputMapping { get; set; }  // Changed from ButtonDescriptiveName to InputMapping
        public int KeyCode { get; set; }
        public string DisplayName { get; set; }
    }
}