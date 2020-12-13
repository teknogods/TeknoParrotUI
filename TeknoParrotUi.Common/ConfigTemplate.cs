using System.Collections.Generic;

namespace TeknoParrotUi.Common
{
    public enum FieldType
    {
        Text = 0,
        Numeric = 1,
        Bool = 2,
        Dropdown = 3,
        Slider = 4
    }
    public class FieldInformation
    {
        public string CategoryName { get; set; }
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public FieldType FieldType { get; set; }
        public int FieldMin { get; set; }
        public int FieldMax { get; set; }
        public List<string> FieldOptions { get; set; }
    }
}
