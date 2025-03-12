using System;
using Avalonia.Media.Imaging;

namespace TeknoParrotUi.ViewModels
{
    public class GameListItem
    {
        public string Name { get; set; }
        public string IconPath { get; set; }

        // Add additional properties that might be needed
        public string GameId { get; set; }
        public string Description { get; set; }
        public bool IsInstalled { get; set; }
    }
}