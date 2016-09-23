using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace GTANetwork.Util
{
    public class StringCache : IDisposable
    {
        public StringCache()
        {
            CachedData = new Dictionary<string, CachedString>();
        }

        public int Timeout = 5;
        private Dictionary<string, CachedString> CachedData;
        public bool createdCache;
        public IntPtr GetCached(string text)
        {
            lock (CachedData)
            {
                CachedString ourString;

                if (!CachedData.ContainsKey(text))
                {
                    ourString = new CachedString();
                    ourString.Allocate(text);
                    CachedData.Add(text, ourString);
                    createdCache = true;
                }
                else
                {
                    ourString = CachedData[text];
                    ourString.LastAccess = DateTime.Now;
                }

                return ourString.Pointer;
            }
        }

        public void Pulse()
        {
            lock (CachedData)
            {
                for (int i = CachedData.Count - 1; i >= 0; i--)
                {
                    if (DateTime.Now.Subtract(CachedData.ElementAt(i).Value.LastAccess).TotalSeconds > Timeout)
                    {
                        CachedData.ElementAt(i).Value.Free();
                        CachedData.Remove(CachedData.ElementAt(i).Key);
                    }
                }
            }
        }
        
        public void Dispose()
        {
            lock (CachedData)
            {
                foreach (var s in CachedData)
                {
                    s.Value.Free();
                }

                CachedData.Clear();
            }
        }
    }

    public class CachedString
    {
        public DateTime LastAccess;
        public string Data;
        public IntPtr Pointer;
        public bool Allocated;

        public void Free()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(Pointer);
                Pointer = IntPtr.Zero;
            }

            Data = null;
            Allocated = false;
        }

        public void Allocate(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text + "\0");
            Pointer = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, Pointer, data.Length);
            Allocated = true;
            Data = text;
            LastAccess = DateTime.Now;
        }
    }
}