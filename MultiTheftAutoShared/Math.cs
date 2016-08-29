using System;
using ProtoBuf;

namespace GTANetworkShared
{
    [ProtoContract]
    [ProtoInclude(4, typeof(Quaternion))]
    public class Vector3
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }

        private static Random randInstance = new Random();

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3(double x, double y, double z)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
        }

        public static bool operator ==(Vector3 left, Vector3 right)
        {
            if ((object)left == null && (object)right == null) return true;
            if ((object)left == null || (object)right == null) return false;
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        }

        public static bool operator !=(Vector3 left, Vector3 right)
        {
            if ((object)left == null && (object)right == null) return false;
            if ((object)left == null || (object)right == null) return true;
            return left.X != right.X || left.Y != right.Y || left.Z != right.Z;
        }

        public static Vector3 operator -(Vector3 left, Vector3 right)
        {
            if ((object)left == null || (object)right == null) return new Vector3();
            return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3 operator +(Vector3 left, Vector3 right)
        {
            if ((object)left == null || (object)right == null) return new Vector3();
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3 operator *(Vector3 left, float right)
        {
            if ((object)left == null) return new Vector3();
            return new Vector3(left.X * right, left.Y * right, left.Z * right);
        }

        public static Vector3 operator /(Vector3 left, float right)
        {
            if ((object)left == null) return new Vector3();
            return new Vector3(left.X / right, left.Y / right, left.Z / right);
        }

        public static Vector3 Lerp(Vector3 start, Vector3 end, float n)
        {
            return new Vector3()
            {
                X = start.X + (end.X - start.X) * n,
                Y = start.Y + (end.Y - start.Y) * n,
                Z = start.Z + (end.Z - start.Z) * n,
            };
        }

        public override string ToString()
        {
            return string.Format("X: {0} Y: {1} Z: {2}", X, Y, Z);
        }

        public float LengthSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        public float Length()
        {
            return (float)Math.Sqrt(LengthSquared());
        }

        public void Normalize()
        {
            var len = Length();

            X = X / len;
            Y = Y / len;
            Z = Z / len;
        }

        public Vector3 Normalized
        {
            get
            {
                var len = Length();

                return new Vector3(X / len, Y / len, Z / len);
            }
        }

        public static Vector3 RandomXY()
        {
            Vector3 v = new Vector3();
            double radian = randInstance.NextDouble() * 2 * Math.PI;

            v.X = (float)Math.Cos(radian);
            v.Y = (float)Math.Sin(radian);
            v.Normalize();

            return v;
        }

        public Vector3 Around(float distance)
        {
            return this + RandomXY() * distance;
        }

        public float DistanceToSquared(Vector3 right)
        {
            if ((object)right == null) return 0f;

            var nX = X - right.X;
            var nY = Y - right.Y;
            var nZ = Z - right.Z;

            return nX * nX + nY * nY + nZ * nZ;
        }

        public float DistanceTo(Vector3 right)
        {
            if ((object) right == null) return 0f;
            return (float)Math.Sqrt(DistanceToSquared(right));
        }

        public float DistanceToSquared2D(Vector3 right)
        {
            if ((object)right == null) return 0f;

            var nX = X - right.X;
            var nY = Y - right.Y;

            return nX * nX + nY * nY;
        }

        public float DistanceTo2D(Vector3 right)
        {
            if ((object)right == null) return 0f;
            return (float)Math.Sqrt(DistanceToSquared2D(right));
        }

        public Vector3()
        { }
    }

    [ProtoContract]
    public class Quaternion : Vector3
    {
        [ProtoMember(1)]
        public float W { get; set; }

        public Quaternion()
        { }

        public override string ToString()
        {
            return string.Format("X: {0} Y: {1} Z: {2} W: {3}", X, Y, Z, W);
        }

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quaternion(double x, double y, double z, double w)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
            W = (float)w;
        }
    }
}