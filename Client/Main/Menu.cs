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
using GTANetwork.Sync;

namespace GTANetwork
{
    internal partial class Main
    {

        public static TabView MainMenu;
        private TabInteractiveListItem _Verified;
        private TabInteractiveListItem _Spotlight;
        private TabInteractiveListItem _serverBrowser;
        private TabInteractiveListItem _lanBrowser;
        private TabInteractiveListItem _favBrowser;
        private TabInteractiveListItem _recentBrowser;
        private TabItemSimpleList _serverPlayers;
        private TabSubmenuItem _serverItem;
        private TabSubmenuItem _connectTab;
        private TabMapItem _mainMapItem;
        private TabTextItem _statsItem;
        private TabWelcomeMessageItem _welcomePage;

        private static List<string> VerifiedList = new List<string>();
        private static List<string> InternetList = new List<string>();

        private bool finished = true;
        private bool _hasScAvatar;

        private bool ListSorting = true;

        private int TotalPlayers;
        private int TotalServers;

        private readonly MenuPool _menuPool;

        public static int BytesSent = 0;
        public static int BytesReceived = 0;

        public static int MessagesSent = 0;
        public static int MessagesReceived = 0;

        public static List<int> AveragePacketSize = new List<int>();

        private int _lastBytesSent;
        private int _lastBytesReceived;
        private DateTime _lastCheck;

        internal static int _bytesSentPerSecond;
        internal static int _bytesReceivedPerSecond;

        private Vector3 _vinewoodSign = new Vector3(827.74f, 1295.68f, 364.34f);

        // STATS
        public static int _bytesSent = 0;
        public static int _bytesReceived = 0;

        public static int _messagesSent = 0;
        public static int _messagesReceived = 0;


        public void GetWelcomeMessage()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (var wc = new ImpatientWebClient())
                    {
                        var rawJson = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim('/') + "/welcome.json");
                        var jsonObj = JsonConvert.DeserializeObject<WelcomeSchema>(rawJson) as WelcomeSchema;
                        if (jsonObj == null) throw new WebException();
                        if (!File.Exists(GTANInstallDir + "images\\" + jsonObj.Picture))
                        {
                            wc.DownloadFile(PlayerSettings.MasterServerAddress.Trim('/') + "/pictures/" + jsonObj.Picture, GTANInstallDir + "\\images\\" + jsonObj.Picture);
                        }

                        _welcomePage.Text = jsonObj.Message;
                        _welcomePage.TextTitle = jsonObj.Title;
                        _welcomePage.PromoPicturePath = GTANInstallDir + "images\\" + jsonObj.Picture;

                        LogManager.RuntimeLog("Set text to " + jsonObj.Message + " and title to " + jsonObj.Title);
                    }
                }
                catch (WebException)
                {
                }
            });
        }

        public void UpdateSocialClubAvatar()
        {
            try
            {
                if (string.IsNullOrEmpty(SocialClubName)) return;

                var uri = "https://a.rsg.sc/n/" + SocialClubName.ToLower();

                using (var wc = new ImpatientWebClient())
                {
                    wc.DownloadFile(uri, GTANInstallDir + "images\\scavatar.png");
                }
                _hasScAvatar = true;
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "UPDATE SC AVATAR");
            }
        }

        private void AddToFavorites(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out int port)) return;
            PlayerSettings.FavoriteServers.Add(server);
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void RemoveFromFavorites(string server)
        {
            PlayerSettings.FavoriteServers.Remove(server);
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void AddServerToRecent(UIMenuItem server)
        {
            if (string.IsNullOrWhiteSpace(server.Description)) return;
            var split = server.Description.Split(':');
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out int tmpPort) || PlayerSettings.RecentServers.Contains(server.Description)) return;
            PlayerSettings.RecentServers.Add(server.Description);
            if (PlayerSettings.RecentServers.Count > 20)
                PlayerSettings.RecentServers.RemoveAt(0);
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");

            var item = new UIMenuItem(server.Text) { Description = server.Description };
            item.SetRightLabel(server.RightLabel);
            item.SetLeftBadge(server.LeftBadge);
            item.Activated += (sender, selectedItem) =>
            {
                if (IsOnServer())
                {
                    Client.Disconnect("Switching servers");

                    if (Npcs != null)
                    {
                        Npcs.ToList().ForEach(pair => pair.Value.Clear());
                        Npcs.Clear();
                    }

                    while (IsOnServer()) Script.Yield();
                }

                var pass = server.LeftBadge == UIMenuItem.BadgeStyle.Lock;

                var splt = server.Description.Split(':');
                if (splt.Length < 2) return;
                if (!int.TryParse(splt[1], out int port)) return;
                ConnectToServer(splt[0], port, pass);
                MainMenu.TemporarilyHidden = true;
                _connectTab.RefreshIndex();
            };
            _recentBrowser.Items.Add(item);
        }

        private void AddServerToRecent(string server, string password)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out int tmpPort)) return;
            if (!PlayerSettings.RecentServers.Contains(server))
            {
                PlayerSettings.RecentServers.Add(server);
                if (PlayerSettings.RecentServers.Count > 20)
                    PlayerSettings.RecentServers.RemoveAt(0);
                Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");

                var item = new UIMenuItem(server);
                item.Description = server;
                item.SetRightLabel(server);
                item.Activated += (sender, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers");
                        NetEntityHandler.ClearAll();


                        if (Npcs != null)
                        {
                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                            Npcs.Clear();
                        }

                        while (IsOnServer()) Script.Yield();
                    }

                    var splt = server.Split(':');
                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;
                    ConnectToServer(splt[0], port, false, password);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                };
                _recentBrowser.Items.Add(item);
            }
        }

        private void RebuildServerBrowser()
        {
            if (!finished) return;
            finished = false;

            _Verified.Items.Clear();
            _serverBrowser.Items.Clear();
            _favBrowser.Items.Clear();
            _lanBrowser.Items.Clear();
            _recentBrowser.Items.Clear();

            _Verified.RefreshIndex();
            _serverBrowser.RefreshIndex();
            _favBrowser.RefreshIndex();
            _lanBrowser.RefreshIndex();
            _recentBrowser.RefreshIndex();

            _currentOnlinePlayers = 0;
            _currentOnlineServers = 0;

            var fetchThread = new Thread((ThreadStart)delegate
            {

                try
                {
                    if (Client == null)
                    {
                        var port = GetOpenUdpPort();
                        if (port == 0)
                        {
                            Util.Util.SafeNotify("No available UDP port was found.");
                            return;
                        }
                        _config.Port = port;
                        Client = new NetClient(_config);
                        Client.Start();
                    }

                    Client.DiscoverLocalPeers(Port);

                    LogManager.RuntimeLog("Contacting " + PlayerSettings.MasterServerAddress);

                    if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                        return;

                    string response = String.Empty;
                    string responseVerified = String.Empty;
                    try
                    {
                        using (var wc = new ImpatientWebClient())
                        {
                            LogManager.RuntimeLog("Downloading response...");
                            response = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim() + "/servers");
                            responseVerified = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim() + "/verified");
                            LogManager.RuntimeLog("Downloaded " + response);
                        }
                    }
                    catch (Exception e)
                    {
                        Util.Util.SafeNotify("~r~~h~ERROR~h~~w~~n~Could not contact master server. Try again later.");
                        var logOutput = "===== EXCEPTION CONTACTING MASTER SERVER @ " + DateTime.UtcNow + " ======\n";
                        logOutput += "Message: " + e.Message;
                        logOutput += "\nData: " + e.Data;
                        logOutput += "\nStack: " + e.StackTrace;
                        logOutput += "\nSource: " + e.Source;
                        logOutput += "\nTarget: " + e.TargetSite;
                        if (e.InnerException != null)
                            logOutput += "\nInnerException: " + e.InnerException.Message;
                        logOutput += "\n";
                        LogManager.SimpleLog("masterserver", logOutput);
                    }

                    var list = new List<string>();
                    var listVerified = new List<string>();

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

                        if (dejson != null && dejson.list != null)
                        {
                            list.AddRange(dejson.list);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(responseVerified))
                    {
                        var dejson = JsonConvert.DeserializeObject<MasterServerList>(responseVerified) as MasterServerList;

                        if (dejson != null && dejson.list != null)
                        {
                            listVerified.AddRange(dejson.list);
                        }
                    }

                    foreach (var server in PlayerSettings.FavoriteServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

                    foreach (var server in PlayerSettings.RecentServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

                    list = list.Distinct().ToList();
                    listVerified = listVerified.Distinct().ToList();

                    foreach (var server in list)
                    {
                        var split = server.Split(':');
                        if (split.Length != 2) continue;
                        int port;
                        if (!int.TryParse(split[1], out port))
                            continue;

                        var item = new UIMenuItem(split[0] + ":" + split[1]);
                        item.Description = split[0] + ":" + split[1];

                        int lastIndx = 0;

                        try
                        {
                            if (!isIPLocal(Dns.GetHostAddresses(split[0])[0].ToString()))
                            {
                                if (_serverBrowser.Items.Count > 0)
                                    lastIndx = _serverBrowser.Index;

                                _serverBrowser.Items.Add(item);
                                _serverBrowser.Index = lastIndx;
                            }
                            else
                            {
                                if (!_lanBrowser.Items.Any(i => i.Description == item.Description))
                                {
                                    if (_lanBrowser.Items.Count > 0)
                                        lastIndx = _lanBrowser.Index;

                                    _lanBrowser.Items.Add(item);
                                    _lanBrowser.Index = lastIndx;
                                }
                            }

                            if (listVerified.Contains(server))
                            {
                                _Verified.Items.Add(item);
                                _Verified.Index = lastIndx;
                            }

                            if (PlayerSettings.RecentServers.Contains(server))
                            {
                                _recentBrowser.Items.Add(item);
                                _recentBrowser.Index = lastIndx;
                            }

                            if (PlayerSettings.FavoriteServers.Contains(server))
                            {
                                _favBrowser.Items.Add(item);
                                _favBrowser.Index = lastIndx;
                            }
                        }
                        catch (Exception e)
                        {
                            LogManager.LogException(e, "DISCOVERY EXCEPTION");
                        }
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i != 0 && i % 10 == 0)
                        {
                            Thread.Sleep(1000);
                        }
                        var spl = list[i].Split(':');
                        if (spl.Length < 2) continue;
                        try
                        {
                            Client.DiscoverKnownPeer(Dns.GetHostAddresses(spl[0])[0].ToString(), int.Parse(spl[1]));
                        }
                        catch (Exception e)
                        {
                            LogManager.LogException(e, "DISCOVERY EXCEPTION");
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.LogException(e, "DISCOVERY CRASH");
                }
                finished = true;
            });

            fetchThread.Start();
        }

        private void RebuildPlayersList()
        {
            _serverPlayers.Dictionary.Clear();

            List<SyncPed> list = null;
            lock (NetEntityHandler)
            {
                list = new List<SyncPed>(NetEntityHandler.ClientMap.Where(pair => pair.Value is SyncPed)
                    .Select(pair => pair.Value).Cast<SyncPed>().Take(10));
            }

            _serverPlayers.Dictionary.Add("Total Players", (list.Count + 1).ToString());

            var us = NetEntityHandler.ClientMap.Values.FirstOrDefault(p => p is RemotePlayer && ((RemotePlayer)p).LocalHandle == -2) as RemotePlayer;

            if (us == null) _serverPlayers.Dictionary.Add(PlayerSettings.DisplayName, ((int)(Latency * 1000)) + "ms");
            else _serverPlayers.Dictionary.Add(us.Name, ((int)(Latency * 1000)) + "ms");

            foreach (var ped in list)
            {
                try
                {
                    if (ped != null)
                    {
                        _serverPlayers.Dictionary.Add(ped.Name, ((int)(ped.Latency * 1000)) + "ms");
                    }

                }
                catch (ArgumentException) { }
            }
        }

        private void BuildMainMenu()
        {
            MainMenu = new TabView("Grand Theft Auto Network");
            MainMenu.CanLeave = false;
            MainMenu.MoneySubtitle = "GTAN " + CurrentVersion;

            _mainMapItem = new TabMapItem();

            #region Welcome Screen
            {
                _welcomePage = new TabWelcomeMessageItem("Welcome to GTA Network", "Join a server on the right! Weekly Updates! Donate, or whatever.");
                MainMenu.Tabs.Add(_welcomePage);
            }
            #endregion

            #region ServerBrowser
            {
                var dConnect = new TabButtonArrayItem("Quick Connect");

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "IP Address";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newIp = InputboxThread.GetUserInput(_clientIp ?? "", TickSpinner);
                        _clientIp = newIp;
                        ipButton.Text = string.IsNullOrWhiteSpace(newIp) ? "IP Address" : newIp;
                        MainMenu.TemporarilyHidden = false;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Port";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var port = InputboxThread.GetUserInput(Port.ToString(), TickSpinner);

                        if (string.IsNullOrWhiteSpace(port))
                        {
                            port = "4499";
                        }

                        int newPort;
                        if (!int.TryParse(port, out newPort))
                        {
                            Util.Util.SafeNotify("Wrong port format!");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        Port = newPort;
                        ipButton.Text = Port.ToString();
                        MainMenu.TemporarilyHidden = false;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Password";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newIp = InputboxThread.GetUserInput("", TickSpinner);
                        MainMenu.TemporarilyHidden = false;
                        _QCpassword = newIp;
                        ipButton.Text = string.IsNullOrWhiteSpace(newIp) ? "Password" : "*******";
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Connect";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        var isPassworded = false;
                        if (!string.IsNullOrWhiteSpace(_QCpassword)) isPassworded = true;

                        AddServerToRecent(_clientIp + ":" + Port, _QCpassword);
                        ConnectToServer(_clientIp, Port, isPassworded, _QCpassword);
                        MainMenu.TemporarilyHidden = true;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                _Verified = new TabInteractiveListItem("Verified", new List<UIMenuItem>());
                _serverBrowser = new TabInteractiveListItem("Internet", new List<UIMenuItem>());
                _favBrowser = new TabInteractiveListItem("Favorites", new List<UIMenuItem>());
                _lanBrowser = new TabInteractiveListItem("Local Network", new List<UIMenuItem>());
                _recentBrowser = new TabInteractiveListItem("Recent", new List<UIMenuItem>());

                _connectTab = new TabSubmenuItem("connect", new List<TabItem>() { dConnect, _favBrowser, _Verified, _serverBrowser, _lanBrowser, _recentBrowser });

                MainMenu.AddTab(_connectTab);
                _connectTab.DrawInstructionalButtons += (sender, args) =>
                {
                    MainMenu.DrawInstructionalButton(4, Control.Jump, "Refresh");

                    if (Game.IsControlJustPressed(Control.Jump))
                    {
                        RebuildServerBrowser();
                    }
                    #region Verified, Iternet, LAN and Recent
                    if (_connectTab.Index == 2 && _connectTab.Items[2].Focused || _connectTab.Index == 3 && _connectTab.Items[3].Focused || _connectTab.Index == 4 && _connectTab.Items[4].Focused || _connectTab.Index == 5 && _connectTab.Items[5].Focused)
                    {
                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Add to Favorites");
                        if (Game.IsControlJustPressed(Control.Enter))
                        {
                            var selectedServer = _serverBrowser.Items[_serverBrowser.Index];
                            if (_connectTab.Index == 2 && _connectTab.Items[2].Focused)
                            {
                                selectedServer = _Verified.Items[_Verified.Index];
                            }
                            else if (_connectTab.Index == 4 && _connectTab.Items[4].Focused)
                            {
                                selectedServer = _lanBrowser.Items[_lanBrowser.Index];
                            }
                            else if (_connectTab.Index == 5 && _connectTab.Items[5].Focused)
                            {
                                selectedServer = _recentBrowser.Items[_recentBrowser.Index];
                            }
                            selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.None);
                            if (PlayerSettings.FavoriteServers.Contains(selectedServer.Description))
                            {
                                var favItem = _favBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description);
                                if (favItem != null)
                                {
                                    selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.None);
                                    RemoveFromFavorites(selectedServer.Description);
                                    _favBrowser.Items.Remove(favItem);
                                    _favBrowser.RefreshIndex();
                                }
                            }
                            else
                            {
                                AddToFavorites(selectedServer.Description);
                                selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.Star);
                                var item = new UIMenuItem(selectedServer.Text);
                                item.Description = selectedServer.Description;
                                item.SetRightLabel(selectedServer.RightLabel);
                                item.SetLeftBadge(selectedServer.LeftBadge);
                                item.Activated += (faf, selectedItem) =>
                                {
                                    if (IsOnServer())
                                    {
                                        Client.Disconnect("Switching servers");
                                        NetEntityHandler.ClearAll();

                                        if (Npcs != null)
                                        {
                                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                            Npcs.Clear();
                                        }

                                        while (IsOnServer()) Script.Yield();
                                    }
                                    bool pass = false;
                                    if (selectedServer.LeftBadge == UIMenuItem.BadgeStyle.Lock)
                                    {
                                        pass = true;
                                    }

                                    var splt = selectedServer.Description.Split(':');

                                    if (splt.Length < 2) return;
                                    int port;
                                    if (!int.TryParse(splt[1], out port)) return;

                                    ConnectToServer(splt[0], port, pass);
                                    MainMenu.TemporarilyHidden = true;
                                    _connectTab.RefreshIndex();
                                };
                                _favBrowser.Items.Add(item);
                            }
                        }
                    }
                    #endregion
                    #region Favorites
                    if (_connectTab.Index == 1 && _connectTab.Items[1].Focused)
                    {
                        MainMenu.DrawInstructionalButton(6, Control.NextCamera, "Add Server");

                        #region Add server
                        if (Game.IsControlJustPressed(Control.NextCamera))
                        {

                            MainMenu.TemporarilyHidden = true;
                            var serverIp = InputboxThread.GetUserInput("Server IP(:Port)", TickSpinner);

                            if (serverIp.Contains("Server IP(:Port)") || string.IsNullOrWhiteSpace(serverIp))
                            {
                                Util.Util.SafeNotify("Incorrect input!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }
                            else if (!serverIp.Contains(":"))
                            {
                                serverIp += ":4499";
                            }
                            MainMenu.TemporarilyHidden = false;

                            if (!PlayerSettings.FavoriteServers.Contains(serverIp))
                            {
                                AddToFavorites(serverIp);
                                var item = new UIMenuItem(serverIp);
                                item.Description = serverIp;
                                _favBrowser.Items.Add(item);
                            }
                            else
                            {
                                Util.Util.SafeNotify("Server already on the list");
                                return;
                            }
                        }
                        #endregion

                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Remove");

                        #region Remove
                        if (Game.IsControlJustPressed(Control.Enter))
                        {
                            var selectedServer = _favBrowser.Items[_favBrowser.Index];
                            var favItem = _favBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description);

                            if (_Verified.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _Verified.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_serverBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _serverBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_lanBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _lanBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_recentBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _recentBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (favItem != null)
                            {
                                Util.Util.SafeNotify("Server removed from Favorites!");
                                RemoveFromFavorites(selectedServer.Description);
                                _favBrowser.Items.Remove(favItem);
                                _favBrowser.RefreshIndex();
                            }
                        }
                        #endregion


                    }
                    #endregion
                };
            }
            #endregion

            #region Settings

            {
                #region General Menu
                var GeneralMenu = new TabInteractiveListItem("General", new List<UIMenuItem>());
                {
                    var nameItem = new UIMenuItem("Display Name");
                    nameItem.SetRightLabel(PlayerSettings.DisplayName);
                    nameItem.Activated += (sender, item) =>
                    {
                        if (IsOnServer())
                        {
                            GTA.UI.Screen.ShowNotification("You cannot change your Display Name while connected to a server.");
                            return;
                        }
                        MainMenu.TemporarilyHidden = true;
                        var newName = InputboxThread.GetUserInput(PlayerSettings.DisplayName ?? "Enter a new Display Name", TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            if (newName.Length > 32)
                                newName = newName.Substring(0, 32);

                            newName = newName.Replace(' ', '_');

                            PlayerSettings.DisplayName = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                        MainMenu.TemporarilyHidden = false;
                    };

                    GeneralMenu.Items.Add(nameItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Show Game FPS Counter", PlayerSettings.ShowFPS);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.ShowFPS = @checked;
                        DebugInfo.ShowFps = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Disable Rockstar Editor", PlayerSettings.DisableRockstarEditor);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.DisableRockstarEditor = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Allow Webcam/Microphone Streaming (Requires restart)", PlayerSettings.MediaStream);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.MediaStream = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var nameItem = new UIMenuItem("Update Channel");
                    nameItem.SetRightLabel(PlayerSettings.UpdateChannel);
                    nameItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newName = InputboxThread.GetUserInput(PlayerSettings.UpdateChannel ?? "stable", TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            PlayerSettings.UpdateChannel = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                        MainMenu.TemporarilyHidden = false;
                    };

                    GeneralMenu.Items.Add(nameItem);
                }
                #endregion

                #region Chat
                var ChatboxMenu = new TabInteractiveListItem("Chat", new List<UIMenuItem>());

                {
                    var chatItem = new UIMenuCheckboxItem("Enable Timestamp", PlayerSettings.Timestamp);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.Timestamp = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                {
                    var chatItem = new UIMenuCheckboxItem("Use Military Time", PlayerSettings.Militarytime);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.Militarytime = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                {
                    var chatItem = new UIMenuCheckboxItem("Scale Chatbox With Safezone", PlayerSettings.ScaleChatWithSafezone);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.ScaleChatWithSafezone = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuItem("Horizontal Chatbox Offset");
                    chatItem.SetRightLabel(PlayerSettings.ChatboxXOffset.ToString());
                    chatItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var strInput = InputboxThread.GetUserInput(PlayerSettings.ChatboxXOffset.ToString(), TickSpinner);


                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.Util.SafeNotify("Input was not in the correct format.");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        MainMenu.TemporarilyHidden = false;
                        PlayerSettings.ChatboxXOffset = newSetting;
                        chatItem.SetRightLabel(PlayerSettings.ChatboxXOffset.ToString());
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuItem("Vertical Chatbox Offset");
                    chatItem.SetRightLabel(PlayerSettings.ChatboxYOffset.ToString());
                    chatItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var strInput = InputboxThread.GetUserInput(PlayerSettings.ChatboxYOffset.ToString(), TickSpinner);


                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.Util.SafeNotify("Input was not in the correct format.");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        MainMenu.TemporarilyHidden = false;
                        PlayerSettings.ChatboxYOffset = newSetting;
                        chatItem.SetRightLabel(PlayerSettings.ChatboxYOffset.ToString());
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuCheckboxItem("Use Classic Chatbox", PlayerSettings.UseClassicChat);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.UseClassicChat = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                #endregion

                #region Experimental
                var ExpMenu = new TabInteractiveListItem("Experimental", new List<UIMenuItem>());
                {

                    var expItem = new UIMenuCheckboxItem("Disable Chromium Embedded Framework (Requires restart)", PlayerSettings.DisableCEF);
                    expItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.DisableCEF = @checked;
                        SaveSettings();
                    };
                    ExpMenu.Items.Add(expItem);
                }
                //{
                //    var expItem = new UIMenuItem("Chromium Embedded Framework Framerate (requires reconnect)");

                //    if(PlayerSettings.CEFfps == 0)
                //    {
                //        expItem.SetRightLabel("Auto (0)");
                //    }
                //    else
                //    {
                //        expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());
                //    }

                //    expItem.Activated += (sender, item) =>
                //    {
                //        MainMenu.TemporarilyHidden = true;
                //        var strInput = InputboxThread.GetUserInput(PlayerSettings.CEFfps.ToString(),
                //            10, TickSpinner);
                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting) || newSetting < 0 || newSetting > 120)
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            MainMenu.TemporarilyHidden = false;
                //            return;
                //        }
                //        expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());
                //        if (newSetting == 0)
                //        {
                //            CEFManager.FPS = (int)Game.FPS;
                //            expItem.SetRightLabel("Auto (0)");
                //        }
                //        else
                //        {
                //            CEFManager.FPS = newSetting;
                //            expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());

                //        }
                //        PlayerSettings.CEFfps = newSetting;
                //        MainMenu.TemporarilyHidden = false;
                //        SaveSettings();
                //    };
                //    ExpMenu.Items.Add(expItem);
                //}

                #endregion

                #region Debug Menu
                var DebugMenu = new TabInteractiveListItem("Debug", new List<UIMenuItem>());
                {
                    {
                        var expItem = new UIMenuCheckboxItem("Enable CEF Development Tools (http://localhost:9222) [Restart required]", PlayerSettings.CEFDevtool);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            PlayerSettings.CEFDevtool = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var expItem = new UIMenuCheckboxItem("Enable Debug mode (Requires an extra tool)", PlayerSettings.DebugMode);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            PlayerSettings.DebugMode = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var expItem = new UIMenuCheckboxItem("Save Debug to File (Huge performance impact)", SaveDebugToFile);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            SaveDebugToFile = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Remove Game Entities", RemoveGameEntities);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            RemoveGameEntities = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Show Streamer Debug Data", DebugInfo.StreamerDebug);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            DebugInfo.StreamerDebug = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Show Player Debug Data", DebugInfo.PlayerDebug);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            DebugInfo.PlayerDebug = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Disable Nametag Draw", ToggleNametagDraw);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            ToggleNametagDraw = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Disable Position Update", TogglePosUpdate);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            TogglePosUpdate = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                }

                #endregion

                var welcomeItem = new TabSubmenuItem("settings", new List<TabItem>()
                {
                    GeneralMenu,
                    ChatboxMenu,
                    //DisplayMenu,
                    //GraphicsMenu,
                    ExpMenu,
                    DebugMenu
                });
                MainMenu.AddTab(welcomeItem);
            }

            #endregion

            #region Host
            {
#if ATTACHSERVER
                var settingsPath = GTANInstallDir + "\\server\\settings.xml";

                if (File.Exists(settingsPath) && Directory.Exists(GTANInstallDir + "\\server\\resources"))
                {
                    var settingsFile = ServerSettings.ReadSettings(settingsPath);

                    var hostStart = new TabTextItem("Start Server", "Host a Session",
                        "Press [ENTER] to start your own server!");
                    hostStart.CanBeFocused = false;

                    hostStart.Activated += (sender, args) =>
                    {
                        if (IsOnServer() || _serverProcess != null)
                        {
                            GTA.UI.Screen.ShowNotification("~b~~h~GTA Network~h~~w~~n~Leave the current server first!");
                            return;
                        }

                        GTA.UI.Screen.ShowNotification("~b~~h~GTA Network~h~~w~~n~Starting server...");
                        var startSettings = new ProcessStartInfo(GTANInstallDir + "\\server\\GTANetworkServer.exe");
                        startSettings.CreateNoWindow = true;
                        startSettings.RedirectStandardOutput = true;
                        startSettings.UseShellExecute = false;
                        startSettings.WorkingDirectory = GTANInstallDir + "\\server";

                        _serverProcess = Process.Start(startSettings);

                        Script.Wait(5000);
                        ConnectToServer("127.0.0.1", settingsFile.Port);
                    };

                    var settingsList = new List<UIMenuItem>();

                    {
                        var serverName = new UIMenuItem("Server Name");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Name);
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Name, 40, TickSpinner);
                            if (string.IsNullOrWhiteSpace(newName))
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~GTA Network~h~~w~~n~Server name must not be empty!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }
                            serverName.SetRightLabel(newName);
                            settingsFile.Name = newName;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Password");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Password);
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Password, 40, TickSpinner);
                            serverName.SetRightLabel(newName);
                            settingsFile.Password = newName;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Player Limit");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.MaxPlayers.ToString());
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.MaxPlayers.ToString(), 40,
                                TickSpinner);
                            int newLimit;
                            if (string.IsNullOrWhiteSpace(newName) ||
                                !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit))
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~GTA Network~h~~w~~n~Invalid input for player limit!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }

                            serverName.SetRightLabel(newName);
                            settingsFile.MaxPlayers = newLimit;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Port");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Port.ToString());
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Port.ToString(), 40, TickSpinner);
                            int newLimit;
                            if (string.IsNullOrWhiteSpace(newName) ||
                                !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit) ||
                                newLimit < 1024)
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~GTA Network~h~~w~~n~Invalid input for server port!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }

                            serverName.SetRightLabel(newName);
                            settingsFile.Port = newLimit;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Announce to Master Server", settingsFile.Announce);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.Announce = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Auto Portforward (UPnP)", settingsFile.UseUPnP);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.UseUPnP = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Use Access Control List", settingsFile.UseACL);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.UseACL = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    var serverSettings = new TabInteractiveListItem("Server Settings", settingsList);

                    var resourcesList = new List<UIMenuItem>();
                    {
                        var resourceRoot = GTANInstallDir + "\\server\\resources";
                        var folders = Directory.GetDirectories(resourceRoot);

                        foreach (var folder in folders)
                        {
                            var resourceName = Path.GetFileName(folder);

                            var item = new UIMenuCheckboxItem(resourceName,
                                settingsFile.Resources.Any(res => res.Path == resourceName));
                            resourcesList.Add(item);
                            item.CheckboxEvent += (sender, @checked) =>
                            {
                                if (@checked)
                                {
                                    settingsFile.Resources.Add(new ServerSettings.SettingsResFilepath()
                                    {
                                        Path = resourceName
                                    });
                                }
                                else
                                {
                                    settingsFile.Resources.Remove(
                                        settingsFile.Resources.FirstOrDefault(r => r.Path == resourceName));
                                }
                                ServerSettings.WriteSettings(settingsPath, settingsFile);
                            };
                        }
                    }

                    var resources = new TabInteractiveListItem("Resources", resourcesList);


                    var welcomeItem = new TabSubmenuItem("host",
                        new List<TabItem> { hostStart, serverSettings, resources });
                    MainMenu.AddTab(welcomeItem);
                }
#endif
            }
            #endregion

            #region Quit
            {
                var welcomeItem = new TabTextItem("Quit", "Quit GTA Network", "Are you sure you want to quit Grand Theft Auto Network and return to desktop?");
                welcomeItem.CanBeFocused = false;
                welcomeItem.Activated += (sender, args) =>
                {
                    if (Client != null && IsOnServer()) Client.Disconnect("Quit");
                    CEFManager.Dispose();
                    CEFManager.DisposeCef();
                    Script.Wait(1000);
                    //Environment.Exit(0);
                    Process.GetProcessesByName("GTA5")[0].Kill();
                    Process.GetCurrentProcess().Kill();
                };
                MainMenu.Tabs.Add(welcomeItem);
            }
            #endregion

            #region Current Server Tab

            #region Players
            _serverPlayers = new TabItemSimpleList("Players", new Dictionary<string, string>());
            #endregion

            var favTab = new TabTextItem("Favorite", "Add to Favorites", "Add the current server to favorites.");
            favTab.CanBeFocused = false;
            favTab.Activated += (sender, args) =>
            {
                var serb = _currentServerIp + ":" + _currentServerPort;
                AddToFavorites(_currentServerIp + ":" + _currentServerPort);
                var item = new UIMenuItem(serb);
                item.Description = serb;
                Util.Util.SafeNotify("Server added to favorites!");
                item.Activated += (faf, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers");

                        NetEntityHandler.ClearAll();

                        if (Npcs != null)
                        {
                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                            Npcs.Clear();
                        }

                        while (IsOnServer()) Script.Yield();
                    }

                    var splt = serb.Split(':');

                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;

                    ConnectToServer(splt[0], port);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                    AddServerToRecent(serb, "");
                };
                _favBrowser.Items.Add(item);
            };

            var dcItem = new TabTextItem("Disconnect", "Disconnect", "Disconnect from the current server.");
            dcItem.CanBeFocused = false;
            dcItem.Activated += (sender, args) =>
            {
                if (Client != null) Client.Disconnect("Quit");
            };

            _statsItem = new TabTextItem("Statistics", "Network Statistics", "");
            _statsItem.CanBeFocused = false;

            _serverItem = new TabSubmenuItem("server", new List<TabItem>() { _serverPlayers, favTab, _statsItem, dcItem });
            _serverItem.Parent = MainMenu;
            #endregion

            MainMenu.RefreshIndex();
        }

        private void RestoreMainMenu()
        {
            MainMenu.TemporarilyHidden = false;
            JustJoinedServer = false;

            MainMenu.Tabs.Remove(_serverItem);
            MainMenu.Tabs.Remove(_mainMapItem);

            if (!MainMenu.Tabs.Contains(_welcomePage))
                MainMenu.Tabs.Insert(0, _welcomePage);

            MainMenu.RefreshIndex();
            _localMarkers.Clear();

        }

        public void PauseMenu()
        {
            if ((Game.IsControlJustPressed(Control.FrontendPauseAlternate) || Game.IsControlJustPressed(Control.FrontendPause)) && !MainMenu.Visible && !_wasTyping)
            {
                MainMenu.Visible = true;

                if (!IsOnServer())
                {
                    if (MainMenu.Visible)
                    {
                        World.RenderingCamera = MainMenuCamera;
                    }
                    else
                    {
                        World.RenderingCamera = null;
                    }

                }
                else if (MainMenu.Visible)
                {
                    RebuildPlayersList();
                }

                MainMenu.RefreshIndex();
            }
            else
            {
                if (!BlockControls)
                {
                    MainMenu.ProcessControls();
                }

                MainMenu.Update();
                MainMenu.CanLeave = IsOnServer();
                if (MainMenu.Visible && !MainMenu.TemporarilyHidden && !_mainMapItem.Focused && _hasScAvatar && File.Exists(GTANInstallDir + "\\images\\scavatar.png"))
                {
                    var safe = new Point(300, 180);
                    Util.Util.DxDrawTexture(0, GTANInstallDir + "images\\scavatar.png", UIMenu.GetScreenResolutionMantainRatio().Width - safe.X - 64, safe.Y - 80, 64, 64, 0, 255, 255, 255, 255, false);
                }

                if (!IsOnServer())
                {
                    Game.EnableControlThisFrame(Control.FrontendPause);
                }
            }
            DEBUG_STEP = 6;

            if (_isGoingToCar && Game.IsControlJustPressed(Control.PhoneCancel))
            {
                Game.Player.Character.Task.ClearAll();
                _isGoingToCar = false;
            }

            if (_mainWarning != null)
            {
                _mainWarning.Draw();
                return;
            }
        }

        public class WelcomeSchema
        {
            public string Title { get; set; }
            public string Message { get; set; }
            public string Picture { get; set; }
        }

        public class MasterServerList
        {
            public List<string> list { get; set; }
            public List<string> listVerified { get; set; }
        }

    }
}