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
        private Thread _httpDownloadThread;
        private bool _cancelDownload;

        private void StartFileDownload(string address)
        {
            _cancelDownload = false;

            _httpDownloadThread?.Abort();
            _httpDownloadThread = new Thread((ThreadStart)delegate
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        var manifestJson = wc.DownloadString(address + "/manifest.json");

                        var obj = JsonConvert.DeserializeObject<FileManifest>(manifestJson);

                        wc.DownloadProgressChanged += (sender, args) =>
                        {
                            _threadsafeSubtitle = "Downloading " + args.ProgressPercentage;
                        };

                        foreach (var resource in obj.exportedFiles)
                        {
                            if (!Directory.Exists(FileTransferId._DOWNLOADFOLDER_ + resource.Key))
                                Directory.CreateDirectory(FileTransferId._DOWNLOADFOLDER_ + resource.Key);

                            for (var index = resource.Value.Count - 1; index >= 0; index--)
                            {
                                var file = resource.Value[index];
                                if (file.type == FileType.Script) continue;

                                var target = Path.Combine(FileTransferId._DOWNLOADFOLDER_, resource.Key, file.path);

                                if (File.Exists(target))
                                {
                                    var newHash = DownloadManager.HashFile(target);

                                    if (newHash == file.hash) continue;
                                }

                                wc.DownloadFileAsync(
                                    new Uri($"{address}/{resource.Key}/{file.path}"), target);

                                while (wc.IsBusy)
                                {
                                    Thread.Yield();
                                    if (!_cancelDownload) continue;
                                    wc.CancelAsync();
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "HTTP FILE DOWNLOAD");
                }
            });
        }

        public static void InvokeFinishedDownload(List<string> resources)
        {
            var confirmObj = Client.CreateMessage();
            confirmObj.Write((byte)PacketType.ConnectionConfirmed);
            confirmObj.Write(true);
            confirmObj.Write(resources.Count);

            for (int i = resources.Count - 1; i >= 0; i--)
            {
                confirmObj.Write(resources[i]);
            }

            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

            HasFinishedDownloading = true;
            Function.Call((Hash)0x10D373323E5B9C0D); //_REMOVE_LOADING_PROMPT
            Function.Call(Hash.DISPLAY_RADAR, true);
        }
    }
}
