using UIForia.Util;
using UnityEngine;

namespace UIForia.Layout {

    public struct Size {

        public float width;
        public float height;

        public Size(float width, float height) {
            this.width = width;
            this.height = height;
        }

        public Size(Vector2 size) {
            this.width = size.x;
            this.height = size.y;
        }

        public static implicit operator Vector2(Size size) {
            return new Vector2(size.width, size.height);
        }

        public static bool operator ==(Size a, Size b) {
            float w = a.width - b.width;
            float h = a.height - b.height;
            float sqrMag = w * w + h * h;
            return sqrMag < 9.99999943962493E-11;
        }

        public static bool operator !=(Size a, Size b) {
            float w = a.width - b.width;
            float h = a.height - b.height;
            float sqrMag = w * w + h * h;
            return sqrMag > 9.99999943962493E-11;
        }

        public bool Equals(Size other) {
            float w = width - other.width;
            float h = height - other.height;
            float sqrMag = w * w + h * h;
            return sqrMag < 9.99999943962493E-11;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj) || !(obj is Size)) {
                return false;
            }
            float w = width - ((Size)obj).width;
            float h = height - ((Size)obj).height;
            float sqrMag = w * w + h * h;
            return sqrMag < 9.99999943962493E-11;
        }

        public override int GetHashCode() {
            return width.GetHashCode() ^ height.GetHashCode() << 2;
        }

        public bool IsDefined() {
            return FloatUtil.IsDefined(width) && FloatUtil.IsDefined(height);
        }

        public static Size Unset => new Size(FloatUtil.UnsetValue, FloatUtil.UnsetValue);

    }

}