using System;
using Rage;
using Rage.Native;

[assembly: Rage.Attributes.Plugin("GTA Network", Author = "Guadmaz")]

namespace GTANetwork
{
    public static class EntryPoint
    {
        private static Main _mainEntry;

        public static void Main()
        {
            _mainEntry = new Main();

            while (true)
            {
                Process();
                GameFiber.Yield();
            }
        }

        public static void Process()
        {
            _mainEntry.OnTick(null, EventArgs.Empty);
        }
    }

    public static class Function
    {
        public static void Call(Hash hash, params NativeArgument[] args)
        {
            NativeFunction.CallByHash<int>((ulong) hash, args);
        }

        public static T Call<T>(Hash hash, params NativeArgument[] args)
        {
            return (T)NativeFunction.CallByHash((ulong) hash, typeof(T), args);
        }
    }
}