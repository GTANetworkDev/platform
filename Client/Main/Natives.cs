using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.GUI;
using GTANetwork.Javascript;
using GTANetwork.Misc;
using GTANetwork.Streamer;
using GTANetwork.Util;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.Win32;
using NativeUI;
using NativeUI.PauseMenu;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork
{
    internal partial class Main
    {
        public static IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(Game.Player.Character.Handle);
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(tmp.X);
                    list.Add(tmp.Y);
                    list.Add(tmp.Z);
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(Game.Player.Handle);
                }
                else if (arg is EntityArgument)
                {
                    list.Add(NetEntityHandler.NetToEntity(((EntityArgument)arg).NetHandle)?.Handle);
                }
                else if (arg is EntityPointerArgument)
                {
                    list.Add(new OutputArgument(NetEntityHandler.NetToEntity(((EntityPointerArgument)arg).NetHandle)));
                }
                else if (args == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(new LocalHandle(Game.Player.Character.Handle));
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new GTANetworkShared.Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(new LocalHandle(Game.Player.Handle));
                }
                else if (arg is EntityArgument)
                {
                    var ent = NetEntityHandler.NetToStreamedItem(((EntityArgument)arg).NetHandle);

                    if (ent == null)
                    {
                        list.Add(new LocalHandle(0));
                    }
                    else if (ent is ILocalHandleable)
                    {
                        list.Add(new LocalHandle(ent.RemoteHandle));
                    }
                    else
                    {
                        list.Add(new LocalHandle(ent.RemoteHandle, HandleType.NetHandle));
                    }
                }
                else if (arg is EntityPointerArgument)
                {
                    list.Add(new OutputArgument(NetEntityHandler.NetToEntity(((EntityPointerArgument)arg).NetHandle)));
                }
                else if (arg is ListArgument)
                {
                    List<object> output = new List<object>();
                    var larg = (ListArgument)arg;
                    if (larg.Data != null && larg.Data.Count > 0)
                        output.AddRange(DecodeArgumentListPure(larg.Data.ToArray()));
                    list.Add(output);
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static List<NativeArgument> ParseNativeArguments(params object[] args)
        {
            var list = new List<NativeArgument>();
            foreach (var o in args)
            {
                if (o is int)
                {
                    list.Add(new IntArgument() { Data = ((int)o) });
                }
                else if (o is uint)
                {
                    list.Add(new UIntArgument() { Data = ((uint)o) });
                }
                else if (o is string)
                {
                    list.Add(new StringArgument() { Data = ((string)o) });
                }
                else if (o is float)
                {
                    list.Add(new FloatArgument() { Data = ((float)o) });
                }
                else if (o is double)
                {
                    list.Add(new FloatArgument() { Data = ((float)(double)o) });
                }
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is GTANetworkShared.Vector3)
                {
                    var tmp = (GTANetworkShared.Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is Vector3)
                {
                    var tmp = (Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
                }
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
                }
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
                else if (o is EntityArgument)
                {
                    list.Add((EntityArgument)o);
                }
                else if (o is EntityPointerArgument)
                {
                    list.Add((EntityPointerArgument)o);
                }
                else if (o is NetHandle)
                {
                    list.Add(new EntityArgument(((NetHandle)o).Value));
                }
                else if (o is LocalHandle)
                {
                    list.Add(new EntityArgument(NetEntityHandler.EntityToNet(((LocalHandle)o).Value)));
                }
                else if (o is IList)
                {
                    var larg = new ListArgument();
                    var l = ((IList)o);
                    object[] array = new object[l.Count];
                    l.CopyTo(array, 0);
                    larg.Data = new List<NativeArgument>(ParseNativeArguments(array));
                    list.Add(larg);
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public void DecodeNativeCall(NativeData obj)
        {
            if (!NativeWhitelist.IsAllowed(obj.Hash) && obj.Internal == false)
            {
                throw new ArgumentException("Hash \"" + obj.Hash.ToString("X") + "\" is not allowed!");
            }
            else if (obj.Hash == (ulong)Hash.REQUEST_SCRIPT_AUDIO_BANK)
            {
                if (!SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[0]).Data))
                {
                    throw new ArgumentException("Such SoundSet is not allowed!");
                }
            }
            else if (obj.Hash == (ulong)Hash.PLAY_SOUND_FRONTEND)
            {
                if (!SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[1]).Data) || !SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[2]).Data))
                {
                    throw new ArgumentException("SoundSet/Name is not allowed!");
                }

            }


            var list = new List<InputArgument>();

            var nativeType = CheckNativeHash(obj.Hash);
            LogManager.DebugLog("NATIVE TYPE IS " + nativeType);
            int playerHealth = Game.Player.Character.Health;

            if (((int)nativeType & (int)Enums.NativeType.VehicleWarp) > 0)
            {
                int veh = ((EntityArgument)obj.Arguments[1]).NetHandle;
                var item = NetEntityHandler.NetToStreamedItem(veh);
                if (item != null && !item.StreamedIn) NetEntityHandler.StreamIn(item);
            }

            if (((int)nativeType & (int)Enums.NativeType.EntityWarp) > 0)
            {
                float x = ((FloatArgument)obj.Arguments[1]).Data;
                float y = ((FloatArgument)obj.Arguments[2]).Data;
                float z = ((FloatArgument)obj.Arguments[3]).Data;

                int interior;
                if ((interior = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, x, y, z)) != 0)
                {
                    Function.Call((Hash)0x2CA429C029CCF247, interior); // LOAD_INTERIOR
                    Function.Call(Hash.SET_INTERIOR_ACTIVE, interior, true);
                    Function.Call(Hash.DISABLE_INTERIOR, interior, false);
                    if (Function.Call<bool>(Hash.IS_INTERIOR_CAPPED, interior))
                        Function.Call(Hash.CAP_INTERIOR, interior, false);
                }
            }

            var objectList = DecodeArgumentList(obj.Arguments.ToArray());

            var enumerable = objectList as object[] ?? objectList.ToArray();
            list.AddRange(enumerable.Select(ob => ob is OutputArgument ? (OutputArgument)ob : new InputArgument(ob)));

            if (enumerable.Any())
                LogManager.DebugLog("NATIVE CALL ARGUMENTS: " + enumerable.Aggregate((f, s) => f + ", " + s) + ", RETURN TYPE: " + obj.ReturnType);
            Model model = null;
            if (((int)nativeType & (int)Enums.NativeType.NeedsModel) > 0)
            {
                LogManager.DebugLog("REQUIRES MODEL");
                int position = 0;
                if (((int)nativeType & (int)Enums.NativeType.NeedsModel1) > 0)
                    position = 0;
                if (((int)nativeType & (int)Enums.NativeType.NeedsModel2) > 0)
                    position = 1;
                if (((int)nativeType & (int)Enums.NativeType.NeedsModel3) > 0)
                    position = 2;
                LogManager.DebugLog("POSITION IS " + position);
                var modelObj = obj.Arguments[position];
                int modelHash = 0;
                if (modelObj is UIntArgument)
                {
                    modelHash = unchecked((int)((UIntArgument)modelObj).Data);
                }
                else if (modelObj is IntArgument)
                {
                    modelHash = ((IntArgument)modelObj).Data;
                }
                LogManager.DebugLog("MODEL HASH IS " + modelHash);
                model = new Model(modelHash);

                if (model.IsValid)
                {
                    LogManager.DebugLog("MODEL IS VALID, REQUESTING");
                    model.Request(10000);
                }
            }

            if (((int)nativeType & (int)Enums.NativeType.NeedsAnimDict) > 0)
            {
                var animDict = ((StringArgument)obj.Arguments[1]).Data;
                Util.Util.LoadDict(animDict);
            }

            if (((int)nativeType & (int)Enums.NativeType.PtfxAssetRequest) != 0)
            {
                var animDict = ((StringArgument)obj.Arguments[0]).Data;

                Util.Util.LoadPtfxAsset(animDict);
                Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, animDict);

                list.RemoveAt(0);
            }

            if (((int)nativeType & (int)Enums.NativeType.ReturnsEntity) > 0)
            {
                var entId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (EntityCleanup) EntityCleanup.Add(entId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, entId);
                }

                if (model != null)
                    model.MarkAsNoLongerNeeded();
                return;
            }

            if (nativeType == Enums.NativeType.ReturnsBlip)
            {
                var blipId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (BlipCleanup) BlipCleanup.Add(blipId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, blipId);
                }
                return;
            }

            if (((int)nativeType & (int)Enums.NativeType.TimeSet) > 0)
            {
                var newHours = ((IntArgument)obj.Arguments[0]).Data;
                var newMinutes = ((IntArgument)obj.Arguments[1]).Data;
                Time = new TimeSpan(newHours, newMinutes, 0);
            }

            if (((int)nativeType & (int)Enums.NativeType.WeatherSet) > 0)
            {
                var newWeather = ((IntArgument)obj.Arguments[0]).Data;
                if (newWeather >= 0 && newWeather < Enums._weather.Length)
                {
                    Weather = Enums._weather[newWeather];
                    Function.Call((Hash)obj.Hash, Enums._weather[newWeather]);
                    return;
                }
            }

            var tmpArgs = obj.Arguments;

            if (!ReplacePointerNatives(obj.Hash, ref list, ref tmpArgs))
                return;

            if (obj.ReturnType == null)
            {
                Function.Call((Hash)obj.Hash, list.ToArray());
            }
            else
            {
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<int>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is UIntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<uint>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is StringArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<string>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is FloatArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<float>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is BooleanArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<bool>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is Vector3Argument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<Vector3>((Hash)obj.Hash, list.ToArray()));
                }
            }

            if (((int)nativeType & (int)Enums.NativeType.PlayerSkinChange) > 0)
            {
                Ped PlayerChar = Game.Player.Character;
                PlayerChar.Style.SetDefaultClothes();
                PlayerChar.MaxHealth = 200;
                PlayerChar.Health = playerHealth;
            }
        }

        public void SendNativeCallResponse(uint id, object response)
        {
            var obj = new NativeResponse();
            obj.Id = id;

            if (response is int)
            {
                obj.Response = new IntArgument() { Data = ((int)response) };
            }
            else if (response is uint)
            {
                obj.Response = new UIntArgument() { Data = ((uint)response) };
            }
            else if (response is string)
            {
                obj.Response = new StringArgument() { Data = ((string)response) };
            }
            else if (response is float)
            {
                obj.Response = new FloatArgument() { Data = ((float)response) };
            }
            else if (response is bool)
            {
                obj.Response = new BooleanArgument() { Data = ((bool)response) };
            }
            else if (response is Vector3)
            {
                var tmp = (Vector3)response;
                obj.Response = new Vector3Argument()
                {
                    X = tmp.X,
                    Y = tmp.Y,
                    Z = tmp.Z,
                };
            }

            var msg = Client.CreateMessage();
            var bin = SerializeBinary(obj);
            msg.Write((byte)PacketType.NativeResponse);
            msg.Write(bin.Length);
            msg.Write(bin);
            Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        private bool ReplacePointerNatives(ulong hash, ref List<InputArgument> list, ref List<NativeArgument> args)
        {
            if (hash == 0x202709F4C58A0424) // _SET_NOTIFICATION_TEXT_ENTRY
            {
                list[0] = new InputArgument("STRING");
                return true;
            }

            if (hash == 0x6C188BE134E074AA && ((StringArgument)args[0]).Data.Length > 99) // ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            {
                list[0] = ((StringArgument)args[0]).Data.Substring(0, 99);
            }

            return true;
        }

        private Enums.NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return Enums.NativeType.Unknown;
                case 0x00A1CADD00108836:
                    return Enums.NativeType.NeedsModel2 | Enums.NativeType.Unknown | Enums.NativeType.NeedsModel | Enums.NativeType.PlayerSkinChange;
                case 0xD49F9B0955C367DE:
                    return Enums.NativeType.NeedsModel2 | Enums.NativeType.NeedsModel | Enums.NativeType.ReturnsEntity;
                case 0x7DD959874C1FD534:
                    return Enums.NativeType.NeedsModel3 | Enums.NativeType.NeedsModel | Enums.NativeType.ReturnsEntity;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return Enums.NativeType.NeedsModel1 | Enums.NativeType.NeedsModel | Enums.NativeType.ReturnsEntity;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return Enums.NativeType.ReturnsEntity;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return Enums.NativeType.ReturnsBlip;
                case 0x47C3B5848C3E45D8:
                    return Enums.NativeType.TimeSet;
                case 0xED712CA327900C8A:
                    return Enums.NativeType.WeatherSet;
                case 0xF75B0D629E1C063D:
                    return Enums.NativeType.VehicleWarp;
                case 0x239A3351AC1DA385:
                    return Enums.NativeType.EntityWarp;
                case 0xEA47FE3719165B94:
                    return Enums.NativeType.NeedsAnimDict;
                case 0x25129531F77B9ED3:
                case 0x0E7E72961BA18619:
                case 0xF56B8137DF10135D:
                case 0xA41B6A43642AC2CF:
                case 0x0D53A3B8DA0809D2:
                case 0xC95EB1DB6E92113D:
                    return Enums.NativeType.PtfxAssetRequest;
            }
        }
    }
}
