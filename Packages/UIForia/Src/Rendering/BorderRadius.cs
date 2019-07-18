using System.Diagnostics;
using JetBrains.Annotations;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Rendering {

    public struct ResolvedBorderRadius {

        public float topLeft;
        public float topRight;
        public float bottomLeft;
        public float bottomRight;

        public ResolvedBorderRadius(float topLeft, float topRight, float bottomLeft, float bottomRight) {
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomRight = bottomRight;
            this.bottomLeft = bottomLeft;
        }

        [DebuggerStepThrough]
        public static implicit operator Vector4(ResolvedBorderRadius radius) {
            return new Vector4(
                FloatUtil.IsDefined(radius.topLeft) ? radius.topLeft : 0,
                FloatUtil.IsDefined(radius.topRight) ? radius.topRight : 0,
                FloatUtil.IsDefined(radius.bottomRight) ? radius.bottomRight : 0,
                FloatUtil.IsDefined(radius.bottomLeft) ? radius.bottomLeft : 0
            );
        }

        public bool IsUniform {
            get { return topLeft == topRight && topLeft == bottomLeft && topLeft == bottomRight; }
        }

        public bool IsZero => topLeft + topRight + bottomLeft + bottomRight == 0;

    }

    public struct BorderRadius {

        public readonly UIFixedLength topLeft;
        public readonly UIFixedLength topRight;
        public readonly UIFixedLength bottomLeft;
        public readonly UIFixedLength bottomRight;

        public BorderRadius(float radius) {
            this.topLeft = radius;
            this.topRight = radius;
            this.bottomRight = radius;
            this.bottomLeft = radius;
        }

        public BorderRadius(float topLeftRight, float bottomLeftRight) {
            this.topLeft = topLeftRight;
            this.topRight = topLeftRight;
            this.bottomRight = bottomLeftRight;
            this.bottomLeft = bottomLeftRight;
        }

        public BorderRadius(float topLeft, float topRight, float bottomRight, float bottomLeft) {
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomRight = bottomRight;
            this.bottomLeft = bottomLeft;
        }

        public BorderRadius(UIFixedLength topLeft, UIFixedLength topRight, UIFixedLength bottomRight, UIFixedLength bottomLeft) {
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomRight = bottomRight;
            this.bottomLeft = bottomLeft;
        }

        [PublicAPI]
        public bool HasTopLeft => topLeft.IsDefined();

        [PublicAPI]
        public bool HasTopRight => topRight.IsDefined();

        [PublicAPI]
        public bool HasBottomLeft => bottomLeft.IsDefined();

        [PublicAPI]
        public bool HasBottomRight => bottomRight.IsDefined();

        [PublicAPI]
        [DebuggerStepThrough]
        public bool IsDefined() {
            return HasTopLeft || HasTopRight || HasBottomRight || HasBottomLeft;
        }

        [PublicAPI]
        [DebuggerStepThrough]
        public bool Equals(BorderRadius other) {
            return topLeft == other.topLeft
                   && topRight == other.topRight
                   && bottomRight == other.bottomRight
                   && bottomLeft == other.bottomLeft;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is BorderRadius && Equals((BorderRadius) obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = topLeft.GetHashCode();
                hashCode = (hashCode * 397) ^ topRight.GetHashCode();
                hashCode = (hashCode * 397) ^ bottomLeft.GetHashCode();
                hashCode = (hashCode * 397) ^ bottomRight.GetHashCode();
                return hashCode;
            }
        }

        [PublicAPI]
        public static BorderRadius Unset => new BorderRadius(FloatUtil.UnsetValue);

        [DebuggerStepThrough]
        public static implicit operator BorderRadius(Vector4 vec4) {
            return new BorderRadius(vec4.x, vec4.y, vec4.z, vec4.w);
        }

        [DebuggerStepThrough]
        public static implicit operator BorderRadius(float val) {
            return new BorderRadius(val);
        }

//        [DebuggerStepThrough]
//        public static implicit operator Vector4(BorderRadius radius) {
//            return new Vector4(
//                FloatUtil.IsDefined(radius.topLeft.value) ? radius.topLeft : 0,
//                FloatUtil.IsDefined(radius.topRight.value) ? radius.topRight : 0,
//                FloatUtil.IsDefined(radius.bottomRight.value) ? radius.bottomRight : 0,
//                FloatUtil.IsDefined(radius.bottomLeft.value) ? radius.bottomLeft : 0
//            );
//        }

        [DebuggerStepThrough]
        public static bool operator ==(BorderRadius self, BorderRadius other) {
            return self.Equals(other);
        }

        [DebuggerStepThrough]
        public static bool operator !=(BorderRadius self, BorderRadius other) {
            return !self.Equals(other);
        }

    }

}