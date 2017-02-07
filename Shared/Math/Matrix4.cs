using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GTANetworkShared
{
    public class Matrix4
    {
        float m00, m01, m02, m03;
        float m10, m11, m12, m13;
        float m20, m21, m22, m23;
        float m30, m31, m32, m33;

        public Matrix4() { }

        public Matrix4(float v)
        {
            m00 = m01 = m02 = m03 = v;
            m10 = m11 = m12 = m13 = v;
            m20 = m21 = m22 = m23 = v;
            m30 = m31 = m32 = m33 = v;
        }

        public Matrix4(double v)
        {
            m00 = m01 = m02 = m03 = (float)v;
            m10 = m11 = m12 = m13 = (float)v;
            m20 = m21 = m22 = m23 = (float)v;
            m30 = m31 = m32 = m33 = (float)v;
        }

        public Matrix4(
            float _m00, float _m01, float _m02, float _m03,
            float _m10, float _m11, float _m12, float _m13,
            float _m20, float _m21, float _m22, float _m23,
            float _m30, float _m31, float _m32, float _m33)
        {
            m00 = _m00; m01 = _m01; m02 = _m02; m03 = _m03;
            m10 = _m10; m11 = _m11; m12 = _m12; m13 = _m13;
            m20 = _m20; m21 = _m21; m22 = _m22; m23 = _m23;
            m30 = _m30; m31 = _m31; m32 = _m32; m33 = _m33;
        }

        public Vector3 Transform(float x, float y, float z)
        {
            return new Vector3(
                (m00 * x) + (m01 * y) + (m02 * z) + m03,
                (m10 * x) + (m11 * y) + (m12 * z) + m13,
                (m20 * x) + (m21 * y) + (m22 * z) + m23
            );
        }

        public Vector3 Transform(double x, double y, double z)
        {
            return new Vector3(
                (m00 * x) + (m01 * y) + (m02 * z) + m03,
                (m10 * x) + (m11 * y) + (m12 * z) + m13,
                (m20 * x) + (m21 * y) + (m22 * z) + m23
            );
        }

        public Vector3 Transform(Vector3 v)
        {
            return Transform(v.X, v.Y, v.Z);
        }

        public Matrix4 Multiply(Matrix4 other)
        {
            return this * other;
        }

        public override string ToString()
        {
            return
                "{" +
                "{" + m00 + ", " + m01 + ", " + m02 + ", " + m03 + "}, " +
                "{" + m10 + ", " + m11 + ", " + m12 + ", " + m13 + "}, " +
                "{" + m20 + ", " + m21 + ", " + m22 + ", " + m23 + "}, " +
                "{" + m30 + ", " + m31 + ", " + m32 + ", " + m33 + "}" +
                "}";
        }

        public static Matrix4 operator *(Matrix4 l, Matrix4 r)
        {
            var ret = new Matrix4();

            ret.m00 = (l.m00 * r.m00) + (l.m01 * r.m10) + (l.m02 * r.m20) + (l.m03 * r.m30);
            ret.m01 = (l.m00 * r.m01) + (l.m01 * r.m11) + (l.m02 * r.m21) + (l.m03 * r.m31);
            ret.m02 = (l.m00 * r.m02) + (l.m01 * r.m12) + (l.m02 * r.m22) + (l.m03 * r.m32);
            ret.m03 = (l.m00 * r.m03) + (l.m01 * r.m13) + (l.m02 * r.m23) + (l.m03 * r.m33);

            ret.m10 = (l.m10 * r.m00) + (l.m11 * r.m10) + (l.m12 * r.m20) + (l.m13 * r.m30);
            ret.m11 = (l.m10 * r.m01) + (l.m11 * r.m11) + (l.m12 * r.m21) + (l.m13 * r.m31);
            ret.m12 = (l.m10 * r.m02) + (l.m11 * r.m12) + (l.m12 * r.m22) + (l.m13 * r.m32);
            ret.m13 = (l.m10 * r.m03) + (l.m11 * r.m13) + (l.m12 * r.m23) + (l.m13 * r.m33);

            ret.m20 = (l.m20 * r.m00) + (l.m21 * r.m10) + (l.m22 * r.m20) + (l.m23 * r.m30);
            ret.m21 = (l.m20 * r.m01) + (l.m21 * r.m11) + (l.m22 * r.m21) + (l.m23 * r.m31);
            ret.m22 = (l.m20 * r.m02) + (l.m21 * r.m12) + (l.m22 * r.m22) + (l.m23 * r.m32);
            ret.m23 = (l.m20 * r.m03) + (l.m21 * r.m13) + (l.m22 * r.m23) + (l.m23 * r.m33);

            ret.m30 = (l.m30 * r.m00) + (l.m31 * r.m10) + (l.m32 * r.m20) + (l.m33 * r.m30);
            ret.m31 = (l.m30 * r.m01) + (l.m31 * r.m11) + (l.m32 * r.m21) + (l.m33 * r.m31);
            ret.m32 = (l.m30 * r.m02) + (l.m31 * r.m12) + (l.m32 * r.m22) + (l.m33 * r.m32);
            ret.m33 = (l.m30 * r.m03) + (l.m31 * r.m13) + (l.m32 * r.m23) + (l.m33 * r.m33);

            return ret;
        }

        public static Matrix4 Identity
        {
            get
            {
                return new Matrix4(
                    1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );
            }
        }

        public static Matrix4 CreateRotationX(double rot) { return CreateRotationX((float)rot); }
        public static Matrix4 CreateRotationX(float rot)
        {
            var ret = Identity;
            ret.m11 = (float)Math.Cos(rot);
            ret.m12 = (float)-Math.Sin(rot);
            ret.m21 = (float)Math.Sin(rot);
            ret.m22 = (float)Math.Cos(rot);
            return ret;
        }

        public static Matrix4 CreateRotationY(double rot) { return CreateRotationY((float)rot); }
        public static Matrix4 CreateRotationY(float rot)
        {
            var ret = Identity;
            ret.m00 = (float)Math.Cos(rot);
            ret.m02 = (float)Math.Sin(rot);
            ret.m20 = (float)-Math.Sin(rot);
            ret.m22 = (float)Math.Cos(rot);
            return ret;
        }

        public static Matrix4 CreateRotationZ(double rot) { return CreateRotationZ((float)rot); }
        public static Matrix4 CreateRotationZ(float rot)
        {
            var ret = Identity;
            ret.m00 = (float)Math.Cos(rot);
            ret.m01 = (float)Math.Sin(rot);
            ret.m10 = (float)-Math.Sin(rot);
            ret.m11 = (float)Math.Cos(rot);
            return ret;
        }

        public static Matrix4 CreateScale(double v) { return CreateScale((float)v); }
        public static Matrix4 CreateScale(float v)
        {
            return CreateScale(v, v, v);
        }

        public static Matrix4 CreateScale(double x, double y, double z) { return CreateScale((float)x, (float)y, (float)z); }
        public static Matrix4 CreateScale(float x, float y, float z)
        {
            var ret = Identity;
            ret.m00 = x;
            ret.m11 = y;
            ret.m22 = z;
            return ret;
        }

        public static Matrix4 CreateTranslation(double x, double y, double z) { return CreateTranslation((float)x, (float)y, (float)z); }
        public static Matrix4 CreateTranslation(float x, float y, float z)
        {
            var ret = Identity;
            ret.m03 = x;
            ret.m13 = y;
            ret.m23 = z;
            return ret;
        }
    }
}
