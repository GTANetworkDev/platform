using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GTANetworkShared
{
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
