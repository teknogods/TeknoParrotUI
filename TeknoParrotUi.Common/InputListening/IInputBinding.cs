namespace TeknoParrotUi.Common.InputListening
{
    public interface IInputBinding
    {
        /// <summary>
        /// Gets the type of input source for this binding
        /// </summary>
        InputSourceType SourceType { get; }

        /// <summary>
        /// Gets a human-readable name for this binding
        /// </summary>
        string DisplayName { get; }
    }
}