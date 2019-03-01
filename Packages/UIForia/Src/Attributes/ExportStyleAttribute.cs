using System;
using UIForia.Rendering;

namespace UIForia.Attributes {

    [AttributeUsage(AttributeTargets.Method)]
    public class ExportStyleAttribute : Attribute {

        public readonly string name;
        public UIStyle cachedStyle;

        public ExportStyleAttribute(string name) {
            this.name = name;
        }

    }
}
