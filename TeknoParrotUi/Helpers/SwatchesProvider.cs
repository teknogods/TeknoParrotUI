using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace TeknoParrotUi.Helpers
{
    public class SwatchColorItem
    {
        public string Name { get; set; }
        public Color Color { get; set; }
    }

    public class SwatchesProvider
    {
        public List<SwatchColorItem> Swatches { get; private set; }

        public SwatchesProvider()
        {
            Swatches = new List<SwatchColorItem>
            {
                new SwatchColorItem { Name = "Red", Color = Colors.Red },
                new SwatchColorItem { Name = "Blue", Color = Colors.Blue },
                new SwatchColorItem { Name = "Green", Color = Colors.Green },
                new SwatchColorItem { Name = "Orange", Color = Colors.Orange },
                new SwatchColorItem { Name = "Purple", Color = Colors.Purple },
                new SwatchColorItem { Name = "Teal", Color = Colors.Teal },
                new SwatchColorItem { Name = "Pink", Color = Colors.Pink },
                // Add more color swatches as needed
            };
        }
    }
}