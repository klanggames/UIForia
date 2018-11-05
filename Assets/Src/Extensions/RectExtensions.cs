using UnityEngine;

namespace Src.Extensions {

    public static class RectExtensions {

        public static Rect Intersect(this Rect rect, Rect other) {
            float xMin = rect.x > other.x ? rect.x : other.x;
            float xMax = rect.x + rect.width < other.x + other.width ? rect.x + rect.width : other.x + other.width;
            float yMin = rect.y > other.y ? rect.y : other.y;
            float yMax = rect.y + rect.height < other.y + other.height ? rect.y + rect.height : other.y + other.height;
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

    }

}