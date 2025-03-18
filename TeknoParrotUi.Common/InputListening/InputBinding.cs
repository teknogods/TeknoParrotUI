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
}