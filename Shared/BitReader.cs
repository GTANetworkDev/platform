using System;

namespace GTANetworkShared
{
    public class BitReader
    {
        ~BitReader()
        {
            _array = null;
        }

        public int CurrentIndex { get; set; }

        private byte[] _array;

        public BitReader(byte[] array)
        {
            CurrentIndex = 0;
            _array = array;
        }

        public bool CanRead(int bytes)
        {
            return _array.Length >= CurrentIndex + bytes;
        }

        public bool ReadBoolean()
        {
            var value = BitConverter.ToBoolean(_array, CurrentIndex);
            CurrentIndex += 1;
            return value;
        }

        public float ReadSingle()
        {
            var value = BitConverter.ToSingle(_array, CurrentIndex);
            CurrentIndex += 4;
            return value;
        }

        public byte ReadByte()
        {
            var value = _array[CurrentIndex];
            CurrentIndex += 1;
            return value;
        }

        public short ReadInt16()
        {
            var value = BitConverter.ToInt16(_array, CurrentIndex);
            CurrentIndex += 2;
            return value;
        }

        public ushort ReadUInt16()
        {
            var value = BitConverter.ToUInt16(_array, CurrentIndex);
            CurrentIndex += 2;
            return value;
        }

        public int ReadInt32()
        {
            var value = BitConverter.ToInt32(_array, CurrentIndex);
            CurrentIndex += 4;
            return value;
        }

        public uint ReadUInt32()
        {
            var value = BitConverter.ToUInt32(_array, CurrentIndex);
            CurrentIndex += 4;
            return value;
        }

        public long ReadInt64()
        {
            var value = BitConverter.ToInt64(_array, CurrentIndex);
            CurrentIndex += 8;
            return value;
        }

        public ulong ReadUInt64()
        {
            var value = BitConverter.ToUInt64(_array, CurrentIndex);
            CurrentIndex += 8;
            return value;
        }
    }
}