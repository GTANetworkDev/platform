using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GTANetwork.Util
{
    public static class Memory
    {
        /// <summary>
        /// Turns a byte pattern in the format "XX XX ?? ?? XX XX" etc to a byte array and a mask array
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <param name="oPattern">The output byte array.</param>
        /// <param name="oMask">The output mask </param>
        /// <exception cref="ArgumentException">
        /// Invalid pattern format if pattern isnt in specified format from above
        /// </exception>
        static void ExtractPattern(string pattern, out byte[] oPattern, out bool[] oMask)
        {
            string[] items = pattern.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] output = new byte[items.Length];
            bool[] mask = new bool[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                string s = items[i];
                if (s.Length > 0 && s.Length < 3)
                {
                    if (s == "?" || s == "??")
                    {
                        output[i] = 0;
                        mask[i] = false;
                    }
                    else
                    {
                        if (byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out output[i]))
                        {
                            mask[i] = true;
                        }
                        else
                        {
                            throw new ArgumentException("Invalid pattern format");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid pattern format");
                }
            }
            oPattern = output;
            oMask = mask;

        }

        /// <summary>
        /// Gets the main module base address
        /// </summary>
        public static IntPtr BaseAddress
        {
            get { return Process.GetCurrentProcess().MainModule.BaseAddress; }
        }

        /// <summary>
        /// Gets the size of the main module
        /// </summary>
        public static int ModuleSize
        {
            get { return Process.GetCurrentProcess().MainModule.ModuleMemorySize; }
        }

        static unsafe IntPtr FindPattern(byte[] pattern, bool[] mask)
        {
            return FindPattern(pattern, mask, BaseAddress, ModuleSize);
        }

        static unsafe IntPtr FindPattern(byte[] pattern, bool[] mask, IntPtr startAddress, int searchSize)
        {
            int maskLength = mask.Length - 1;
            byte* address = (byte*)startAddress.ToPointer();
            byte* end = address + searchSize - maskLength;

            for (int i = 0; address < end; address++)
            {
                if (*address == pattern[i] || !mask[i])
                {
                    if (i == maskLength)
                    {
                        return new IntPtr(address - maskLength);
                    }
                    i++;
                }
                else
                {
                    i = 0;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Finds the address of a byte pattern in the format "XX XX ?? ?? XX" etc in the Main Module of the current process
        /// </summary>
        /// <param name="pattern">The pattern to find</param>
        /// <returns></returns>
        public static IntPtr FindPattern(string pattern)
        {
            byte[] pBytes;
            bool[] pBool;
            try
            {
                ExtractPattern(pattern, out pBytes, out pBool);
            }
            catch
            {
                return IntPtr.Zero;
            }
            return FindPattern(pBytes, pBool);
        }

        /// <summary>
        /// Finds the address of a byte pattern in the format "XX XX ?? ?? XX" etc in the range of addresses specified
        /// Memory exceptions will be thrown if the addresses specified arent inside the current processes allocated memory
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="searchSize">Size of the search.</param>
        /// <returns></returns>
        public static IntPtr FindPattern(string pattern, IntPtr startAddress, int searchSize)
        {
            byte[] pBytes;
            bool[] pBool;
            try
            {
                ExtractPattern(pattern, out pBytes, out pBool);
            }
            catch
            {
                return IntPtr.Zero;
            }
            return FindPattern(pBytes, pBool, startAddress, searchSize);
        }

        public static unsafe IntPtr ReadPtr(IntPtr ptr)
        {
            return new IntPtr(*(long*)ptr.ToPointer());
        }

        public static unsafe int ReadInt(IntPtr ptr)
        {
            return *(int*)ptr.ToPointer();
        }

        public static unsafe short ReadShort(IntPtr ptr)
        {
            return *(short*)ptr.ToPointer();
        }

        public static unsafe byte ReadByte(IntPtr ptr)
        {
            return *(byte*)ptr.ToPointer();
        }

        public static unsafe long ReadLong(IntPtr ptr)
        {
            return *(long*)ptr.ToPointer();
        }

        public static unsafe uint ReadUInt(IntPtr ptr)
        {
            return *(uint*)ptr.ToPointer();
        }

        public static unsafe ushort ReadUShort(IntPtr ptr)
        {
            return *(ushort*)ptr.ToPointer();
        }

        public static unsafe sbyte ReadSByte(IntPtr ptr)
        {
            return *(sbyte*)ptr.ToPointer();
        }

        public static unsafe ulong ReadULong(IntPtr ptr)
        {
            return *(ulong*)ptr.ToPointer();
        }

        public static void ReadBytes(IntPtr ptr, byte[] buffer, int count, int offset)
        {
            Marshal.Copy(ptr, buffer, offset, count);
        }

        public static void ReadBytes(IntPtr ptr, byte[] buffer)
        {
            Marshal.Copy(ptr, buffer, 0, buffer.Length);
        }

        public static unsafe void WriteInt(IntPtr ptr, int value)
        {
            *(int*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteShort(IntPtr ptr, short value)
        {
            *(short*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteByte(IntPtr ptr, byte value)
        {
            *(byte*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteLong(IntPtr ptr, long value)
        {
            *(long*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteUInt(IntPtr ptr, uint value)
        {
            *(uint*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteUShort(IntPtr ptr, ushort value)
        {
            *(ushort*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteSByte(IntPtr ptr, sbyte value)
        {
            *(sbyte*)ptr.ToPointer() = value;
        }

        public static unsafe void WriteULong(IntPtr ptr, ulong value)
        {
            *(ulong*)ptr.ToPointer() = value;
        }

        public static void WriteBytes(IntPtr ptr, byte[] buffer, int count, int offset)
        {
            Marshal.Copy(buffer, offset, ptr, count);
        }
        public static void WriteBytes(IntPtr ptr, byte[] buffer)
        {
            Marshal.Copy(buffer, 0, ptr, buffer.Length);
        }
    }

    public static unsafe class ScriptTable
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x10)]
        public struct ScriptTableItem
        {
            [FieldOffset(0x0)]
            public long ScriptStartAddress;
            [FieldOffset(0xC)]
            public int ScriptHash;
        }

        private static ScriptTableItem** itemsPtr;
        private static int* count;

        public static int Count { get { return *count; } }
        public static IntPtr GetScriptAddress(int hash)
        {
            if (!IsTableInitialised) return IntPtr.Zero;
            ScriptTableItem* items = *itemsPtr;
            for (int i = 0; i < *count; i++)
            {
                if (items[i].ScriptHash == hash)
                {
                    return new IntPtr(items[i].ScriptStartAddress);
                }
            }
            return IntPtr.Zero;
        }

        public static bool DoesScriptExist(int hash)
        {
            if (!IsTableInitialised) return false;
            ScriptTableItem* items = *itemsPtr;
            for (int i = 0; i < *count; i++)
            {
                if (items[i].ScriptHash == hash)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsScriptLoaded(int hash)
        {
            if (!IsTableInitialised) return false;
            ScriptTableItem* items = *itemsPtr;
            for (int i = 0; i < *count; i++)
            {
                if (items[i].ScriptHash == hash)
                {
                    return items[i].ScriptStartAddress != 0;
                }
            }
            return false;
        }

        public static bool IsTableInitialised
        {
            get { return (long)itemsPtr != 0; }
        }
        static unsafe ScriptTable()
        {
            IntPtr TablePtr = Memory.FindPattern("48 03 15 ?? ?? ?? ?? 4C 23 C2 49 8B 08");
            IntPtr address = TablePtr + *(int*)(TablePtr + 3) + 7;
            itemsPtr = (ScriptTableItem**)address.ToPointer();
            count = (int*)(address + 0x18);

        }
    }
    public static class GTAMemory
    {
        private static IntPtr GlobalAddress;

        static unsafe GTAMemory()
        {
            IntPtr GlobalPattern = Memory.FindPattern("4C 8D 05 ?? ?? ?? ?? 4D 8B 08 4D 85 C9 74 11");

            GlobalAddress = GlobalPattern + *(int*)(GlobalPattern + 3) + 7;
        }

        public static unsafe IntPtr GetGlobalAddress(int index)
        {
            long** addr = (long**)GlobalAddress.ToPointer();
            return new IntPtr(&addr[index >> 18][index & 0x3FFFF]);
        }
    }
}