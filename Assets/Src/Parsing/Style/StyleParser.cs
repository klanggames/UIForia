using System.Xml.Linq;
using Rendering;
using Src.Style;

namespace Src.Parsing.Style {

    public static class StyleParser {

        public static void ParseStyle(XElement root, UIStyle style) {
            if (root == null) return;
            
            StyleParseUtil.ParseMeasurement(ref style.rect.x, root.GetChild("Rect.X"));
            StyleParseUtil.ParseMeasurement(ref style.rect.y, root.GetChild("Rect.Y"));
            StyleParseUtil.ParseMeasurement(ref style.rect.width, root.GetChild("Rect.W"));
            StyleParseUtil.ParseMeasurement(ref style.rect.height, root.GetChild("Rect.H"));
            
            StyleParseUtil.ParseColor(ref style.paint.backgroundColor, root.GetChild("Paint.BackgroundColor"));
            
        }

    }

}