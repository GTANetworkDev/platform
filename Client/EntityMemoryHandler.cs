//#define DEBUG_MEMORY_STRUCT

#if DEBUG_MEMORY_STRUCT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using NativeUI;
using Control = GTA.Control;

namespace GTANetwork
{
    public class debugscript : Script
    {
        public debugscript()
        {
            Tick += tick;
            MS = new MemScanner();
        }

        public static Vector3 offset = new Vector3();
        public static float Range = 10f;
        public static float Intensity = 10f;
        public static MemScanner MS;

        private static EntityMemoryHandler _vehHandler;
        private static Vehicle _lastCar;
        private static int _lightIndex;

        public unsafe void tick(object sender, EventArgs e)
        {
            var car = Main.PlayerChar.CurrentVehicle;
            MS.CurrentEntity = Main.PlayerChar;
            MS.Update();

            if (car != null)
            {
                if (car != _lastCar)
                {
                    _vehHandler = new EntityMemoryHandler(car, 4096);
                }


                if (Game.IsKeyPressed(Keys.Left) && _lightIndex > 0)
                {
                    _lightIndex--;
                    GTA.UI.Screen.ShowSubtitle("LightIndex: " + _lightIndex, 2000);
                }

                if (Game.IsKeyPressed(Keys.Right))
                {
                    _lightIndex++;
                    GTA.UI.Screen.ShowSubtitle("LightIndex: " + _lightIndex, 2000);
                }

                if (Game.IsKeyPressed(Keys.NumPad0))
                {
                    _vehHandler.RedumpMemory();
                    var mem = _vehHandler.GetMemory();

                    //int offset = 2112;
                    int offset = 1916;

                    var oldComponentDamages = BitConverter.ToUInt64(mem, offset);
                    //var component = (ulong)VehicleComponents.All;
                    var component = (ulong)(1 << _lightIndex);

                    GTA.UI.Screen.ShowSubtitle("LightIndex: " + _lightIndex + " Activated: " + !((oldComponentDamages & component) > 0), 2000);

                    var newValue = oldComponentDamages ^ component;

                    var newBytes = BitConverter.GetBytes(newValue);

                    Marshal.Copy(newBytes, 0, new IntPtr(car.MemoryAddress) + offset, newBytes.Length);
                }
            }

            _lastCar = car;
        }
    }


    public class EntityMemoryHandler
    {
        private byte[] _entityMemory;

        public Entity Entity;
        public int MemorySize;

        public EntityMemoryHandler(Entity ent, int memSize)
        {
            MemorySize = memSize;
            Entity = ent;

            RedumpMemory();
        }

        public EntityMemoryHandler(Entity ent) : this(ent, 4096) { }

        public byte[] GetMemory()
        {
            return _entityMemory;
        }

        public unsafe void RedumpMemory()
        {
            var add = new IntPtr(Entity.MemoryAddress);

            var scanner = new SigScan(Process.GetProcessesByName("GTA5")[0], add, MemorySize);

            scanner.DumpMemory();

            _entityMemory = scanner.GetDumpedMemory();
        }
    }

    public class MemScanner
    {
        public const int MAX_VEHICLE_LEN = 8192;

        public MemScanner()
        {
            _changed = new List<int>();

            for (int i = 0; i < MAX_VEHICLE_LEN; i++)
            {
                _changed.Add(i);
            }

            _lastEntityInts = new int[MAX_VEHICLE_LEN];

            for (int i = 0; i < _lastEntityInts.Length; i++)
            {
                _lastEntityInts[i] = int.MinValue;
            }
        }

        private void GameOnRawFrameRender()
        {
            if (LastEntityMemory != null && _changed != null)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("_checkStay: " + _checkStay + " _checkChange: " + _checkChange + " _checkLastValue: " + _checkLastValue + "\n\n");

                for (int i = _startIndex; i < _startIndex + 40; i++)
                {
                    sb.Append(i.ToString("x") + " - " + LastEntityMemory[i] + "\n");
                }

                if (sb.Length > 0)
                    new UIResText(sb.ToString().Substring(0, Math.Min(sb.Length, 198)), new Point(20, 10), 0.3f, Color.White).Draw();


                StringBuilder sb2 = new StringBuilder();

                sb2.Append("\n\n");

                for (int i = 0; i < Math.Min(40, _changed.Count); i++)
                {
                    sb2.Append(_changed[i].ToString("x") + " - " + LastEntityMemory[_changed[i]]+ "\n");
                }

                if (sb2.Length > 0)
                    new UIResText(sb2.ToString().Substring(0, Math.Min(sb2.Length, 198)), new Point(500, 10), 0.3f, Color.White).Draw();

                if (Game.IsControlJustPressed(0, Control.InteractionMenu))
                {
                    var sb3 = new StringBuilder();

                    for (int i = 0; i < _changed.Count; i++)
                    {
                        sb3.Append(_changed[i].ToString("x") + " - " + LastEntityMemory[_changed[i]] + "\n");
                    }

                    File.WriteAllText("memorymap.txt", sb3.ToString());
                    GTA.UI.Screen.ShowNotification("Written mem map!");
                }

                if (Game.IsControlPressed(0, Control.PhoneDown) && _startIndex < LastEntityMemory.Length - 4)
                {
                    _startIndex += 1;
                }

                if (Game.IsControlPressed(0, Control.PhoneUp) && _startIndex > 0)
                {
                    _startIndex -= 1;
                }
            }
        }

        #region Needed stuff
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int PROCESS_WM_READ = 0x0010;

        public struct MEMORY_BASIC_INFORMATION
        {
            public int BaseAddress;
            public int AllocationBase;
            public int AllocationProtect;
            public int RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }

        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
        #endregion

        private Entity _currentEntity;
        public Entity CurrentEntity
        {
            get { return _currentEntity; }
            set
            {
                if (_currentEntity != value && value != null)
                {
                    OnVehicleChange(value);
                }

                _currentEntity = value;
            }
        }

        public byte[] LastEntityMemory = null;

        private int[] _lastEntityInts = null;
        private List<int> _changed = new List<int>();

        private int _startIndex = 0;
        private Entity _lastEntity;

        private bool _checkChange;
        private bool _checkStay = true;
        private bool _checkLastValue = true;

        public List<byte> ReadMemory(IntPtr address, long len)
        {
            var sys_info = new SYSTEM_INFO();
            GetSystemInfo(out sys_info);

            long startPoint = (long)(((long)address) + len);

            Process proccess = Process.GetProcessesByName("GTA5")[0];

            IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, proccess.Id);



            //int bytesRead = 0;
            var outputList = new List<byte>();
            int bytesRead = 0;

            while ((long)address <= startPoint + len)
            {
                var mem_basic_info = new MEMORY_BASIC_INFORMATION();

                VirtualQueryEx(processHandle, address, out mem_basic_info, 28);

                GTA.UI.Screen.ShowSubtitle(address + " / " + (startPoint + len) + " (" + ((long)address / (startPoint + len)).ToString("P1") + ") -- sizeof: " + mem_basic_info.RegionSize, 1000);
                Script.Yield();

                byte[] buffer = new byte[(uint)mem_basic_info.RegionSize];

                ReadProcessMemory((int)processHandle, mem_basic_info.BaseAddress, buffer, buffer.Length, ref bytesRead);

                outputList.AddRange(buffer);

                address += bytesRead;
                //address = new IntPtr(address);
            }

            return outputList;
        }

        public unsafe void OnVehicleChange(Entity newVehicle)
        {
            GTA.UI.Screen.ShowNotification("ADD: " + new IntPtr(newVehicle.MemoryAddress));

            var scan = new SigScan(Process.GetProcessesByName("GTA5")[0], new IntPtr(newVehicle.MemoryAddress), MAX_VEHICLE_LEN);

            scan.DumpMemory();

            LastEntityMemory = scan.GetDumpedMemory();

        }

        public void Update()
        {
            //var currentVeh = Main.PlayerChar.CurrentVehicle;

            if (Game.IsControlJustPressed(0, Control.Jump))
            {
                _changed = new List<int>();

                for (int i = 0; i < _lastEntityInts.Length; i++)
                {
                    _changed.Add(i);
                }
            }

            if (Game.IsControlJustPressed(0, Control.ThrowGrenade))
            {
                _checkStay = !_checkStay;
                _checkChange = !_checkChange;
            }

            if (Game.IsControlJustPressed(0, Control.VehicleDuck))
            {
                _checkLastValue = !_checkLastValue;
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                for (int i = 0; i < LastEntityMemory.Length; i += 1)
                {
                    int val = LastEntityMemory[i];

                    if (_lastEntityInts[i] != int.MinValue)
                    {
                        var changed = _lastEntityInts[i] != val;

                        if (_checkChange && !changed)
                        {
                            if (_changed.Contains(i)) _changed.Remove(i);
                        }

                        if (_checkStay && changed)
                        {
                            if (_changed.Contains(i)) _changed.Remove(i);
                        }
                    }

                    _lastEntityInts[i] = val;
                }
            }

            if (CurrentEntity != null)
            {
                OnVehicleChange(CurrentEntity);

                if (_checkLastValue)
                    for (int i = 0; i < LastEntityMemory.Length; i += 1)
                    {
                        int val = LastEntityMemory[i];

                        if (_lastEntityInts[i] != int.MinValue)
                        {
                            var changed = _lastEntityInts[i] != val;

                            if (_checkChange && !changed)
                            {
                                if (_changed.Contains(i)) _changed.Remove(i);
                            }

                            if (_checkStay && changed)
                            {
                                if (_changed.Contains(i)) _changed.Remove(i);
                            }
                        }

                        _lastEntityInts[i] = val;
                    }
            }

            _lastEntity = CurrentEntity;

            GameOnRawFrameRender();
        }
    }

    /*
    public class MemScanner
    {
        public const int MAX_VEHICLE_LEN = 8192;

        public MemScanner()
        {
            _changed = new List<int>();

            for (int i = 0; i < MAX_VEHICLE_LEN / 4; i++)
            {
                _changed.Add(i*4);
            }

            _lastEntityInts = new int[MAX_VEHICLE_LEN / 4];

            for (int i = 0; i < _lastEntityInts.Length; i++)
            {
                _lastEntityInts[i] = int.MinValue;
            }
        }

        private void GameOnRawFrameRender()
        {
            if (LastEntityMemory != null && _changed != null)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("_checkStay: " + _checkStay + " _checkChange: " + _checkChange + " _checkLastValue: " + _checkLastValue + "\n\n");

                for (int i = _startIndex; i < _startIndex + 40 * 4; i += 4)
                {
                    sb.Append(i.ToString("x") + " - " + BitConverter.ToInt32(LastEntityMemory, i) + "\n");
                }

                if (sb.Length > 0)
                new UIResText(sb.ToString().Substring(0, Math.Min(sb.Length, 198)), new  Point(20, 10), 0.3f, Color.White).Draw();


                StringBuilder sb2 = new StringBuilder();

                sb2.Append("\n\n");

                for (int i = 0; i < Math.Min(40, _changed.Count); i++)
                {
                    sb2.Append(_changed[i].ToString("x") + " - " + BitConverter.ToInt32(LastEntityMemory, _changed[i]) + "\n");
                }

                if (sb2.Length > 0)
                    new UIResText(sb2.ToString().Substring(0, Math.Min(sb2.Length, 198)), new Point(500, 10), 0.3f, Color.White).Draw();

                if (Game.IsControlJustPressed(0, Control.InteractionMenu))
                {
                    File.WriteAllText("memorymap.txt", sb2.ToString());
                    GTA.UI.Screen.ShowNotification("Written mem map!");
                }

                if (Game.IsControlPressed(0, Control.PhoneDown) && _startIndex < LastEntityMemory.Length - 4)
                {
                    _startIndex += 4;
                }

                if (Game.IsControlPressed(0, Control.PhoneUp) && _startIndex > 0)
                {
                    _startIndex -= 4;
                }
            }
        }

        #region Needed stuff
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int PROCESS_WM_READ = 0x0010;

        public struct MEMORY_BASIC_INFORMATION
        {
            public int BaseAddress;
            public int AllocationBase;
            public int AllocationProtect;
            public int RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }

        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
        #endregion

        private Entity _currentEntity;
        public Entity CurrentEntity
        {
            get { return _currentEntity; }
            set
            {
                if (_currentEntity != value && value != null)
                {
                    OnVehicleChange(value);
                }

                _currentEntity = value;
            }
        }

        public byte[] LastEntityMemory = null;

        private int[] _lastEntityInts = null;
        private List<int> _changed = new List<int>();

        private int _startIndex = 0;
        private Entity _lastEntity;

        private bool _checkChange;
        private bool _checkStay = true;
        private bool _checkLastValue = true;

        public List<byte> ReadMemory(IntPtr address, long len)
        {
            var sys_info = new SYSTEM_INFO();
            GetSystemInfo(out sys_info);

            long startPoint = (long)(((long)address) + len);

            Process proccess = Process.GetProcessesByName("GTA5")[0];

            IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, proccess.Id);



            //int bytesRead = 0;
            var outputList = new List<byte>();
            int bytesRead = 0;

            while ((long)address <= startPoint + len)
            {
                var mem_basic_info = new MEMORY_BASIC_INFORMATION();

                VirtualQueryEx(processHandle, address, out mem_basic_info, 28);

                GTA.UI.Screen.ShowSubtitle(address + " / " + (startPoint + len) + " (" + ((long)address / (startPoint + len)).ToString("P1") + ") -- sizeof: " + mem_basic_info.RegionSize, 1000);
                Script.Yield();

                byte[] buffer = new byte[(uint)mem_basic_info.RegionSize];

                ReadProcessMemory((int)processHandle, mem_basic_info.BaseAddress, buffer, buffer.Length, ref bytesRead);

                outputList.AddRange(buffer);

                address += bytesRead;
                //address = new IntPtr(address);
            }

            return outputList;
        }

        public unsafe void OnVehicleChange(Entity newVehicle)
        {
            GTA.UI.Screen.ShowNotification("ADD: " + new IntPtr(newVehicle.MemoryAddress));

            var scan = new SigScan(Process.GetProcessesByName("GTA5")[0], new IntPtr(newVehicle.MemoryAddress), MAX_VEHICLE_LEN);

            scan.DumpMemory();

            LastEntityMemory = scan.GetDumpedMemory();

        }

        public void Update()
        {
            //var currentVeh = Main.PlayerChar.CurrentVehicle;

            if (Game.IsControlJustPressed(0, Control.Jump))
            {
                _changed = new List<int>();

                for (int i = 0; i < _lastEntityInts.Length; i++)
                {
                    _changed.Add(i * 4);
                }
            }

            if (Game.IsControlJustPressed(0, Control.ThrowGrenade))
            {
                _checkStay = !_checkStay;
                _checkChange = !_checkChange;
            }

            if (Game.IsControlJustPressed(0, Control.VehicleDuck))
            {
                _checkLastValue = !_checkLastValue;
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                for (int i = 0; i < LastEntityMemory.Length; i += 4)
                {
                    int val = BitConverter.ToInt32(LastEntityMemory, i);

                    if (_lastEntityInts[i / 4] != int.MinValue)
                    {
                        var changed = _lastEntityInts[i / 4] != val;

                        if (_checkChange && !changed)
                        {
                            if (_changed.Contains(i)) _changed.Remove(i);
                        }

                        if (_checkStay && changed)
                        {
                            if (_changed.Contains(i)) _changed.Remove(i);
                        }
                    }

                    _lastEntityInts[i / 4] = val;
                }
            }

            if (CurrentEntity != null)
            {
                OnVehicleChange(CurrentEntity);

                if (_checkLastValue)
                    for (int i = 0; i < LastEntityMemory.Length; i += 4)
                    {
                        int val = BitConverter.ToInt32(LastEntityMemory, i);

                        if (_lastEntityInts[i / 4] != int.MinValue)
                        {
                            var changed = _lastEntityInts[i / 4] != val;

                            if (_checkChange && !changed)
                            {
                                if (_changed.Contains(i)) _changed.Remove(i);
                            }

                            if (_checkStay && changed)
                            {
                                if (_changed.Contains(i)) _changed.Remove(i);
                            }
                        }

                        _lastEntityInts[i / 4] = val;
                    }
            }

            _lastEntity = CurrentEntity;

            GameOnRawFrameRender();
        }
    }

    */
}

#endif