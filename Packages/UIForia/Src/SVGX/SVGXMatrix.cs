using Unity.Mathematics;
using UnityEngine;

namespace SVGX {

    public struct SVGXMatrix {

        public readonly float m0;
        public readonly float m1;
        public readonly float m2;
        public readonly float m3;
        public readonly float m4;
        public readonly float m5;

        public SVGXMatrix(float m0, float m1, float m2, float m3, float m4, float m5) {
            this.m0 = m0;
            this.m1 = m1;
            this.m2 = m2;
            this.m3 = m3;
            this.m4 = m4;
            this.m5 = m5;
        }

        public static SVGXMatrix identity => new SVGXMatrix(1, 0, 0, 1, 0, 0);

        public Vector2 position => new Vector2(m4, m5);

        public Vector2 scale => new Vector2(Mathf.Sqrt(m0 * m0 + m1 * m1), Mathf.Sqrt(m2 * m2 + m3 * m3));

        public float skewX {
            get {
                Vector2 px = DeltaTransformPoint(new Vector2(0, 1));
                return (180 / Mathf.PI) * Mathf.Atan2(px.y, px.x) - 90;
            }
        }

        public float skewY {
            get {
                Vector2 py = DeltaTransformPoint(new Vector2(1, 0));
                return (180 / Mathf.PI) * Mathf.Atan2(py.y, py.x);
            }
        }

        public float rotation => skewX;

        private Vector2 DeltaTransformPoint(Vector2 point) {
            return new Vector2(point.x * m0 + point.y * m2, point.x * m1 + point.y * m3);
        }

        public SVGXMatrix Multiply(in SVGXMatrix secondMatrix) {
            float sa = secondMatrix.m0;
            float sb = secondMatrix.m1;
            float sc = secondMatrix.m2;
            float sd = secondMatrix.m3;
            float se = secondMatrix.m4;
            float sf = secondMatrix.m5;
            return new SVGXMatrix(
                m0 * sa + m2 * sb,
                m1 * sa + m3 * sb,
                m0 * sc + m2 * sd,
                m1 * sc + m3 * sd,
                m0 * se + m2 * sf + m4,
                m1 * se + m3 * sf + m5
            );
        }

        public static SVGXMatrix operator *(in SVGXMatrix left, in SVGXMatrix right) {
            return new SVGXMatrix(
                left.m0 * right.m0 + left.m2 * right.m1,
                left.m1 * right.m0 + left.m3 * right.m1,
                left.m0 * right.m2 + left.m2 * right.m3,
                left.m1 * right.m2 + left.m3 * right.m3,
                left.m0 * right.m4 + left.m2 * right.m5 + left.m4,
                left.m1 * right.m4 + left.m3 * right.m5 + left.m5
            );
        }

        public SVGXMatrix Inverse() {
            float det = m0 * m3 - m2 * m1;
            if (det == 0.0f) {
                return this;
            }

            return new SVGXMatrix(
                (m3 / det),
                (-m1 / det),
                (-m2 / det),
                (m0 / det),
                ((m2 * m5 - m4 * m3) / det),
                ((m4 * m1 - m0 * m5) / det)
            );
        }

        public static SVGXMatrix Translation(Vector2 translation) {
            return new SVGXMatrix(1, 0, 0, 1, translation.x, translation.y);
        }

        public SVGXMatrix Scale(float scaleFactor) {
            return new SVGXMatrix(m0 * scaleFactor, m1 * scaleFactor, m2 * scaleFactor, m3 * scaleFactor, m4, m5);
        }

        public SVGXMatrix Scale(float scaleFactorX, float scaleFactorY) {
            return new SVGXMatrix(m0 * scaleFactorX, m1 * scaleFactorX, m2 * scaleFactorY, m3 * scaleFactorY, m4, m5);
        }

        public SVGXMatrix Scale(Vector2 scaleFactor) {
            return new SVGXMatrix(m0 * scaleFactor.x, m1 * scaleFactor.x, m2 * scaleFactor.y, m3 * scaleFactor.y, m4, m5);
        }

        public SVGXMatrix Rotate(float angle) {
            float ca = Mathf.Cos(angle * Mathf.Deg2Rad);
            float sa = Mathf.Sin(angle * Mathf.Deg2Rad);

            return new SVGXMatrix((m0 * ca + m2 * sa), (m1 * ca + m3 * sa), (m2 * ca - m0 * sa), (m3 * ca - m1 * sa), m4, m5);
        }

        public SVGXMatrix Translate(float x, float y) {
            return new SVGXMatrix(m0, m1, m2, m3, m0 * x + m2 * y + m4, m1 * x + m3 * y + m5);
        }

        public SVGXMatrix Translate(Vector2 position) {
            return new SVGXMatrix(m0, m1, m2, m3, m0 * position.x + m2 * position.y + m4, m1 * position.x + m3 * position.y + m5);
        }

        public SVGXMatrix SkewX(float angle) {
            float ta = Mathf.Tan(angle * Mathf.Deg2Rad);
            return new SVGXMatrix(m0, m1, (m2 + m0 * ta), (m3 + m1 * ta), m4, m5);
        }

        public SVGXMatrix SkewY(float angle) {
            float ta = Mathf.Tan(angle * Mathf.Deg2Rad);
            return new SVGXMatrix((m0 + m2 * ta), (m1 + m3 * ta), m2, m3, m4, m5);
        }

        public Vector2 Transform(Vector2 point) {
            return new Vector2(m0 * point.x + m2 * point.y + m4, m1 * point.x + m3 * point.y + m5);
        }

        public Vector3 Transform(Vector3 point) {
            return new Vector3(m0 * point.x + m2 * point.y + m4, m1 * point.x + m3 * point.y + m5, point.z);
        }

        // todo this is silly for most cases since its * 1 or * 0 or + 0, compiler probably collapses it
        public static SVGXMatrix TRS(Vector2 position, float rotation, Vector2 scale) {
            const float a = 1;
            const float b = 0;
            const float c = 0;
            const float d = 1;
            const float e = 0;
            const float f = 0;
            float ca = Mathf.Cos(rotation * Mathf.Deg2Rad);
            float sa = Mathf.Sin(rotation * Mathf.Deg2Rad);
            return new SVGXMatrix(
                (a * ca + c * sa) * scale.x,
                (b * ca + d * sa) * scale.x,
                (c * ca - a * sa) * scale.y,
                (d * ca - b * sa) * scale.y,
                a * position.x + c * position.y + e,
                b * position.x + d * position.y + f
            );
        }

        public static SVGXMatrix TRS(float positionX, float positionY, float rotation, float scaleX, float scaleY) {
            const float a = 1;
            const float b = 0;
            const float c = 0;
            const float d = 1;
            const float e = 0;
            const float f = 0;
            float ca = math.cos(rotation * Mathf.Deg2Rad);
            float sa = math.sin(rotation * Mathf.Deg2Rad);
            return new SVGXMatrix(
                (a * ca + c * sa) * scaleX,
                (b * ca + d * sa) * scaleX,
                (c * ca - a * sa) * scaleY,
                (d * ca - b * sa) * scaleY,
                a * positionX + c * positionY + e,
                b * positionX + d * positionY + f
            );
        }

        public static SVGXMatrix TranslateScale(float positionX, float positionY, float scaleX, float scaleY) {
            const float a = 1;
            const float b = 0;
            const float c = 0;
            const float d = 1;
            const float e = 0;
            const float f = 0;
            const float ca = 1;
            const float sa = 0;
            return new SVGXMatrix(
                (a * ca + c * sa) * scaleX,
                (b * ca + d * sa) * scaleX,
                (c * ca - a * sa) * scaleY,
                (d * ca - b * sa) * scaleY,
                a * positionX + c * positionY + e,
                b * positionX + d * positionY + f
            );
        }

        public Matrix4x4 ToMatrix4x4() {
            Matrix4x4 matrix = Matrix4x4.identity;

            matrix[0, 0] = m0;
            matrix[0, 1] = m1;
            matrix[1, 0] = m2;
            matrix[1, 1] = m3;
            matrix[2, 0] = m4;
            matrix[2, 1] = m5;

            return matrix;
        }

        public override string ToString() {
            string output = string.Format("[SVGXMatrix] a: {0}, b: {1}, c: {2}, d: {3}, e: {4}, f: {5}", m0, m1, m2, m3, m4, m5);
            output += string.Format("\nposition: {0}, rotation: {1}, skewX: {2}, skewY: {3}, scale: {4}", position, rotation, skewX, skewY, scale);
            return output;
        }

    }

}