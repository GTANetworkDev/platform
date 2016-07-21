using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.GUI;
using GTANetworkShared;
using Lidgren.Network;
using Microsoft.Win32;
using NativeUI;
using NativeUI.PauseMenu;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public class MessagePump : Script
    {
        public MessagePump()
        {
            Tick += (sender, args) =>
            {
                if (Main.Client != null)
                {
                    List<NetIncomingMessage> messages = new List<NetIncomingMessage>();
                    int msgsRead = Main.Client.ReadMessages(messages);
                    LogManager.DebugLog("READING " + msgsRead + " MESSAGES");
                    if (msgsRead > 0)
                        foreach (var message in messages)
                        {
                            if (CrossReference.EntryPoint.IsMessageTypeThreadsafe(message.MessageType))
                            {
                                var message1 = message;
                                var pcMsgThread = new Thread((ThreadStart)delegate
                                {
                                    CrossReference.EntryPoint.ProcessMessages(message1, false);
                                });
                                pcMsgThread.IsBackground = true;
                                pcMsgThread.Start();
                            }
                            else
                            {
                                CrossReference.EntryPoint.ProcessMessages(message, true);
                            }
                        }
                }
            };
        }
    }

    public static class CrossReference
    {
        public static Main EntryPoint;
    }

    public class Main : Script
    {
        public static PlayerSettings PlayerSettings;
        
        public static readonly ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9;
        

        public static bool BlockControls;
        public static bool WriteDebugLog;
        public static bool SlowDownClientForDebug;
        public static bool Multithreading;

        public static bool IsSpectating;

        public static Streamer NetEntityHandler;

        private readonly MenuPool _menuPool;

        private UIResText _verionLabel = new UIResText("GTAN " + CurrentVersion.ToString(), new Point(), 0.35f, Color.FromArgb(100, 200, 200, 200));

        private string _clientIp;
        public static ClassicChat Chat;

        public static NetClient Client;
        private static NetPeerConfiguration _config;
        public static ParseableVersion CurrentVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());
        
        public static SynchronizationMode GlobalSyncMode;
        public static bool LerpRotaion = true;
        public static bool LagCompensation = true;
        public static bool RemoveGameEntities = true;
        public static bool ChatVisible = true;

        public static int LocalTeam = -1;
        public static int LocalDimension = 0;
        public int SpectatingEntity;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private bool _lastDead;
        private bool _lastKilled;
        private bool _wasTyping;

        public static TabView MainMenu;
        
        private DebugWindow _debug;
        private SyncEventWatcher Watcher;

        private Vector3 _vinewoodSign = new Vector3(827.74f, 1295.68f, 364.34f);

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;

        private static List<int> _averagePacketSize = new List<int>();

        private TabTextItem _statsItem;
        //
      
        public Main()
        {
            //var scal = new Scaleform(0);
            //scal.Load("hud");
            //scal.CallFunction("MULTIPLAYER_IS_ACTIVE", true);

            CrossReference.EntryPoint = this;

            PlayerSettings = Util.ReadSettings(GTANInstallDir + "\\settings.xml");
            GameSettings = GTANetwork.GameSettings.LoadGameSettings();
            _threadJumping = new Queue<Action>();

            NetEntityHandler = new Streamer();

            Watcher = new SyncEventWatcher(this);

            Names = new Dictionary<int, string>();
            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            EntityCleanup = new List<int>();
            BlipCleanup = new List<int>();
            
            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++) _emptyVehicleMods.Add(i, 0);

            Chat = new ClassicChat();
            Chat.OnComplete += (sender, args) =>
            {
                var message = GTANetwork.Chat.SanitizeString(Chat.CurrentInput);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    JavascriptHook.InvokeMessageEvent(message);

                    var obj = new ChatData()
                    {
                        Message = message,
                    };
                    var data = SerializeBinary(obj);

                    var msg = Client.CreateMessage();
                    msg.Write((int)PacketType.ChatData);
                    msg.Write(data.Length);
                    msg.Write(data);
                    Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int) ConnectionChannel.Chat);
                }
                Chat.IsFocused = false;
            };

            Tick += OnTick;
            KeyDown += OnKeyDown;

            KeyUp += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape && _wasTyping)
                {
                    _wasTyping = false;
                }
            };

            _config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK");
            _config.Port = 8888;
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);


            #region Menu Set up
            _menuPool = new MenuPool();
            BuildMainMenu();
            #endregion

            _debug = new DebugWindow();

            Function.Call((Hash)0x0888C3502DBBEEF5); // _LOAD_MP_DLC_MAPS
            Function.Call((Hash)0x9BAE5AD2508DF078, true); // _ENABLE_MP_DLC_MAPS
            
            MainMenuCamera = World.CreateCamera(new Vector3(743.76f, 1070.7f, 350.24f), new Vector3(),
                GameplayCamera.FieldOfView);
            MainMenuCamera.PointAt(new Vector3(707.86f, 1228.09f, 333.66f));

            RelGroup = World.AddRelationshipGroup("SYNCPED");
            FriendRelGroup = World.AddRelationshipGroup("SYNCPED_TEAMMATES");
            World.SetRelationshipBetweenGroups(Relationship.Neutral, RelGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Neutral, Game.Player.Character.RelationshipGroup, RelGroup);

            World.SetRelationshipBetweenGroups(Relationship.Companion, FriendRelGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Companion, Game.Player.Character.RelationshipGroup, FriendRelGroup);

            //Function.Call(Hash.SHUTDOWN_LOADING_SCREEN);

            GetWelcomeMessage();

            UpdateSocialClubAvatar();

            //CEFManager.Initialize(Game.ScreenResolution);
        }

        public static int RelGroup;
        public static int FriendRelGroup;
        public static bool HasFinishedDownloading;

        // Debug stuff
        private bool display;
        private Ped mainPed;
        private Vehicle mainVehicle;

        private Vector3 oldplayerpos;
        private bool _lastJumping;
        private bool _lastShooting;
        private bool _lastAiming;
        private uint _switch;
        private bool _lastVehicle;
        private bool _oldChat;
        private bool _isGoingToCar;
        //

        public static bool JustJoinedServer { get; set; }
        private int _currentOnlinePlayers;
        private int _currentOnlineServers;

        private TabInteractiveListItem _serverBrowser;
        private TabInteractiveListItem _lanBrowser;
        private TabInteractiveListItem _favBrowser;
        private TabInteractiveListItem _recentBrowser;

        private TabItemSimpleList _serverPlayers;
        private TabSubmenuItem _serverItem;
        private TabSubmenuItem _connectTab;

        private TabWelcomeMessageItem _welcomePage;

        private Process _serverProcess;

        private int _currentServerPort;
        private string _currentServerIp;
        private bool _debugWindow;

        public static Dictionary<int, string> Names;
        public static Dictionary<string, SyncPed> Npcs;
        public static float Latency;
        private int Port = 4499;

        private GameSettings.Settings GameSettings;

        public static Camera MainMenuCamera;
        

        public static string GTANInstallDir = ((string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "GTANetworkInstallDir", null)) ?? AppDomain.CurrentDomain.BaseDirectory;
        
        public void GetWelcomeMessage()
        {
            try
            {
                using (var wc = new ImpatientWebClient())
                {
                    var rawJson = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim('/') + "/welcome.json");
                    var jsonObj = JsonConvert.DeserializeObject<WelcomeSchema>(rawJson) as WelcomeSchema;
                    if (jsonObj == null) throw new WebException();
                    if (!File.Exists(GTANInstallDir + "\\images\\" + jsonObj.Picture))
                    {
                        wc.DownloadFile(PlayerSettings.MasterServerAddress.Trim('/') + "/pictures/" + jsonObj.Picture, GTANInstallDir + "\\images\\" + jsonObj.Picture);
                    }
                    
                    _welcomePage.Text = jsonObj.Message;
                    _welcomePage.TextTitle = jsonObj.Title;
                    _welcomePage.PromoPicturePath = GTANInstallDir + "\\images\\" + jsonObj.Picture;
                }
            }
            catch (WebException ex)
            {
            }
        }

        public void UpdateSocialClubAvatar()
        {
            try
            {
                var scName = Game.Player.Name;

                if (string.IsNullOrEmpty(scName)) return;

                var uri = "https://a.rsg.sc/n/" + scName.ToLower();

                using (var wc = new ImpatientWebClient())
                {
                    wc.DownloadFile(uri, GTANInstallDir  + "\\images\\scavatar.png");
                }
            }
            catch
            {}
        }

        private void AddToFavorites(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            int port;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out port)) return;
            PlayerSettings.FavoriteServers.Add(server);
            Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void RemoveFromFavorites(string server)
        {
            PlayerSettings.FavoriteServers.Remove(server);
            Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void SaveSettings()
        {
            Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void AddServerToRecent(UIMenuItem server)
        {
            if (string.IsNullOrWhiteSpace(server.Description)) return;
            var split = server.Description.Split(':');
            int tmpPort;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out tmpPort)) return;
            if (!PlayerSettings.RecentServers.Contains(server.Description))
            {
                PlayerSettings.RecentServers.Add(server.Description);
                if (PlayerSettings.RecentServers.Count > 20)
                    PlayerSettings.RecentServers.RemoveAt(0);
                Util.SaveSettings(GTANInstallDir + "\\settings.xml");

                var item = new UIMenuItem(server.Text);
                item.Description = server.Description;
                item.SetRightLabel(server.RightLabel);
                item.SetLeftBadge(server.LeftBadge);
                item.Activated += (sender, selectedItem) =>
                        {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers.");

                        if (Npcs != null)
                        {
                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                            Npcs.Clear();
                        }

                        while (IsOnServer()) Script.Yield();
                    }

                    if (server.LeftBadge == UIMenuItem.BadgeStyle.Lock)
                    {
                        _password = Game.GetUserInput(256);
                    }

                    var splt = server.Description.Split(':');
                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;
                    ConnectToServer(splt[0], port);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                };
                _recentBrowser.Items.Add(item);
            }
        }

        private void AddServerToRecent(string server, string password)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            int tmpPort;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out tmpPort)) return;
            if (!PlayerSettings.RecentServers.Contains(server))
            {
                PlayerSettings.RecentServers.Add(server);
                if (PlayerSettings.RecentServers.Count > 20)
                    PlayerSettings.RecentServers.RemoveAt(0);
                Util.SaveSettings(GTANInstallDir + "\\settings.xml");

                var item = new UIMenuItem(server);
                item.Description = server;
                item.SetRightLabel(server);
                item.Activated += (sender, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers.");

                        NetEntityHandler.ClearAll();


                        if (Npcs != null)
                        {
                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                            Npcs.Clear();
                        }

                        while (IsOnServer()) Script.Yield();
                    }

                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        _password = Game.GetUserInput(256);
                    }

                    var splt = server.Split(':');
                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;
                    ConnectToServer(splt[0], port);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                };
                _recentBrowser.Items.Add(item);
            }
        }

        private bool isIPLocal(string ipaddress)
        {
            String[] straryIPAddress = ipaddress.ToString().Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                int[] iaryIPAddress = new int[]
                {
                    int.Parse(straryIPAddress[0], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[1], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[2], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[3], CultureInfo.InvariantCulture)
                };
                if (iaryIPAddress[0] == 10 || iaryIPAddress[0] == 127 ||
                    (iaryIPAddress[0] == 192 && iaryIPAddress[1] == 168) ||
                    (iaryIPAddress[0] == 172 && (iaryIPAddress[1] >= 16 && iaryIPAddress[1] <= 31)))
                {
                    return true;
                }
                else
                {
                    // IP Address is "probably" public. This doesn't catch some VPN ranges like OpenVPN and Hamachi.
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private void RebuildServerBrowser()
        {
            _serverBrowser.Items.Clear();
            _favBrowser.Items.Clear();
            _lanBrowser.Items.Clear();
            _recentBrowser.Items.Clear();

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
                            Util.SafeNotify("No available UDP port was found.");
                            return;
                        }
                        _config.Port = port;
                        Client = new NetClient(_config);
                        Client.Start();
                    }

                    Client.DiscoverLocalPeers(Port);

                    if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                        return;
                    string response = String.Empty;
                    try
                    {
                        using (var wc = new ImpatientWebClient())
                        {
                            response = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim() + "/servers");
                        }
                    }
                    catch (Exception e)
                    {
                        Util.SafeNotify("~r~~h~ERROR~h~~w~~n~Could not contact master server. Try again later.");
                        var logOutput = "===== EXCEPTION CONTACTING MASTER SERVER @ " + DateTime.UtcNow + " ======\n";
                        logOutput += "Message: " + e.Message;
                        logOutput += "\nData: " + e.Data;
                        logOutput += "\nStack: " + e.StackTrace;
                        logOutput += "\nSource: " + e.Source;
                        logOutput += "\nTarget: " + e.TargetSite;
                        if (e.InnerException != null)
                            logOutput += "\nInnerException: " + e.InnerException.Message;
                        logOutput += "\n";
                        File.AppendAllText("scripts\\GTACOOP.log", logOutput);
                    }

                    var list = new List<string>();

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

                        if (dejson != null)
                        {
                            list.AddRange(dejson.list);
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

                    foreach (var server in list)
                    {
                        var split = server.Split(':');
                        if (split.Length != 2) continue;
                        int port;
                        if (!int.TryParse(split[1], out port))
                            continue;

                        var item = new UIMenuItem(server);
                        item.Description = server;

                        int lastIndx = 0;

                        if (!isIPLocal(split[0]))
                        {
                            if (_serverBrowser.Items.Count > 0)
                                lastIndx = _serverBrowser.Index;

                            _serverBrowser.Items.Add(item);
                            _serverBrowser.Index = lastIndx;
                        }
                        else
                        {
                            if (_lanBrowser.Items.Count > 0)
                                lastIndx = _lanBrowser.Index;

                            _lanBrowser.Items.Add(item);
                            _lanBrowser.Index = lastIndx;
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

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i != 0 && i%10 == 0)
                        {
                            Thread.Sleep(3000);
                        }
                        var spl = list[i].Split(':');
                        if (spl.Length < 2) continue;
                        try
                        {
                            Client.DiscoverKnownPeer(spl[0], int.Parse(spl[1]));
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
            });

            fetchThread.Start();
        }
        
        private void RebuildPlayersList()
        {
            _serverPlayers.Dictionary.Clear();

            List<SyncPed> list = null;
            lock (NetEntityHandler)
            {
                list = new List<SyncPed>(NetEntityHandler.ClientMap.Where(pair => pair is SyncPed).Cast<SyncPed>());
            }
            
            _serverPlayers.Dictionary.Add("Total Players", (list.Count + 1).ToString());

            _serverPlayers.Dictionary.Add(PlayerSettings.DisplayName, ((int)(Latency * 1000)) + "ms");

            foreach (var ped in list)
            {
                _serverPlayers.Dictionary.Add(ped.Name + " (" + ped.RemoteHandle + ")", ((int)(ped.Latency * 1000)) + "ms");
            }
        }

        private void TickSpinner()
        {
            OnTick(null, EventArgs.Empty);
        }

        private TabMapItem _mainMapItem;
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
                        var newIp = InputboxThread.GetUserInput(_clientIp ?? "", 30, TickSpinner);
                        _clientIp = newIp;
                        ipButton.Text = string.IsNullOrWhiteSpace(newIp) ? "IP Address" : newIp;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Port";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        var newIp = InputboxThread.GetUserInput(Port.ToString(), 30, TickSpinner);

                        if (string.IsNullOrWhiteSpace(newIp)) return;

                        int newPort;
                        if (!int.TryParse(newIp, out newPort))
                        {
                            Util.SafeNotify("Wrong port format!");
                            return;
                        }
                        Port = newPort;
                        ipButton.Text = newIp;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Password";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        var newIp = InputboxThread.GetUserInput("", 30, TickSpinner);
                        _password = newIp;
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
                        AddServerToRecent(_clientIp + ":" + Port, _password);
                        ConnectToServer(_clientIp, Port);
                        MainMenu.TemporarilyHidden = true;
                    };
                    dConnect.Buttons.Add(ipButton);
                }
                
                _serverBrowser = new TabInteractiveListItem("Internet", new List<UIMenuItem>());
                _lanBrowser = new TabInteractiveListItem("Local Area Network", new List<UIMenuItem>());
                _favBrowser = new TabInteractiveListItem("Favorites", new List<UIMenuItem>());
                _recentBrowser = new TabInteractiveListItem("Recent", new List<UIMenuItem>());
                
                _connectTab = new TabSubmenuItem("connect", new List<TabItem>() { dConnect, _serverBrowser, _lanBrowser, _favBrowser, _recentBrowser });
                MainMenu.AddTab(_connectTab);
                _connectTab.DrawInstructionalButtons += (sender, args) =>
                {
                    MainMenu.DrawInstructionalButton(4, Control.Jump, "Refresh");

                    if (Game.IsControlJustPressed(0, Control.Jump))
                    {
                        RebuildServerBrowser();
                    }

                    if (_connectTab.Index == 1 && _connectTab.Items[1].Focused)
                    {
                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Favorite");
                        if (Game.IsControlJustPressed(0, Control.Enter))
                        {
                            var selectedServer = _serverBrowser.Items[_serverBrowser.Index];
                            selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.None);
                            if (PlayerSettings.FavoriteServers.Contains(selectedServer.Description))
                            {
                                RemoveFromFavorites(selectedServer.Description);
                                var favItem = _favBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description);
                                if (favItem != null)
                                {
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
                                        Client.Disconnect("Switching servers.");

                                        NetEntityHandler.ClearAll();

                                        if (Npcs != null)
                                        {
                                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                            Npcs.Clear();
                                        }

                                        while (IsOnServer()) Script.Yield();
                                    }

                                    if (selectedServer.LeftBadge == UIMenuItem.BadgeStyle.Lock)
                                    {
                                        _password = Game.GetUserInput(256);
                                    }

                                    var splt = selectedServer.Description.Split(':');

                                    if (splt.Length < 2) return;
                                    int port;
                                    if (!int.TryParse(splt[1], out port)) return;
                                    
                                    ConnectToServer(splt[0], port);
                                    MainMenu.TemporarilyHidden = true;
                                    _connectTab.RefreshIndex();
                                    AddServerToRecent(selectedServer);
                                };
                                _favBrowser.Items.Add(item);
                            }
                        }
                    }

                    if (_connectTab.Index == 3 && _connectTab.Items[3].Focused)
                    {
                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Favorite by IP");
                        if (Game.IsControlJustPressed(0, Control.Enter))
                        {
                            var serverIp = InputboxThread.GetUserInput("Server IP:Port", 40, TickSpinner);

                            if (!serverIp.Contains(":"))
                            {
                                Util.SafeNotify("Server IP and port need to be separated by a : character!");
                                return;
                            }

                            if (!PlayerSettings.FavoriteServers.Contains(serverIp))
                            {
                                AddToFavorites(serverIp);
                                var item = new UIMenuItem(serverIp);
                                item.Description = serverIp;
                                item.Activated += (faf, selectedItem) =>
                                {
                                    if (IsOnServer())
                                    {
                                        Client.Disconnect("Switching servers.");

                                        NetEntityHandler.ClearAll();

                                        if (Npcs != null)
                                        {
                                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                            Npcs.Clear();
                                        }

                                        while (IsOnServer()) Script.Yield();
                                    }

                                    var splt = serverIp.Split(':');

                                    if (splt.Length < 2) return;
                                    int port;
                                    if (!int.TryParse(splt[1], out port)) return;

                                    ConnectToServer(splt[0], port);
                                    MainMenu.TemporarilyHidden = true;
                                    _connectTab.RefreshIndex();
                                    AddServerToRecent(serverIp, "");
                                };
                                _favBrowser.Items.Add(item);
                            }
                        }
                    }
                };
            }
            #endregion

            #region Settings

            {
                var internetServers = new TabInteractiveListItem("Multiplayer", new List<UIMenuItem>());

                {
                    var nameItem = new UIMenuItem("Name");
                    nameItem.SetRightLabel(PlayerSettings.DisplayName);
                    nameItem.Activated += (sender, item) =>
                    {
                        if (IsOnServer()) return;
                        var newName = InputboxThread.GetUserInput(PlayerSettings.DisplayName ?? "Enter new name", 40, TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            PlayerSettings.DisplayName = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                    };

                    internetServers.Items.Add(nameItem);
                }

                #if DEBUG
                {
                    
                    var debugItem = new UIMenuCheckboxItem("Debug", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        display = @checked;
                        if (!display)
                        {
                            if (mainPed != null) mainPed.Delete();
                            if (mainVehicle != null) mainVehicle.Delete();
                            if (_debugSyncPed != null)
                            {
                                _debugSyncPed.Clear();
                                _debugSyncPed = null;
                            }
                        }
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Debug Window", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        _debugWindow = @checked;
                    };
                    internetServers.Items.Add(debugItem);
                }
                
                {
                    var debugItem = new UIMenuCheckboxItem("Despawn Entities", RemoveGameEntities);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        RemoveGameEntities = @checked;
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Write Debug Info To File", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        WriteDebugLog = @checked;
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Break Every Update For Debugging", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        SlowDownClientForDebug = @checked;
                    };
                    internetServers.Items.Add(debugItem);
                }
#endif
                {
                    var debugItem = new UIMenuCheckboxItem("Scale Chatbox With Safezone", PlayerSettings.ScaleChatWithSafezone);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.ScaleChatWithSafezone = @checked;
                        SaveSettings();
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Use Experimental Lag Compensation", LagCompensation);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        LagCompensation = @checked;
                    };
                    internetServers.Items.Add(debugItem);
                }
                
                {
                    var debugItem = new UIMenuCheckboxItem("Hide Map Nametags When Zoomed Out", PlayerSettings.HideNametagsWhenZoomedOutMap);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.HideNametagsWhenZoomedOutMap = @checked;
                        SaveSettings();
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Disable Rockstar Editor", PlayerSettings.DisableRockstarEditor);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.DisableRockstarEditor = @checked;
                        SaveSettings();
                    };
                    internetServers.Items.Add(debugItem);
                }

                {
                    var nameItem = new UIMenuItem("Update Channel");
                    nameItem.SetRightLabel(PlayerSettings.UpdateChannel);
                    nameItem.Activated += (sender, item) =>
                    {
                        var newName = InputboxThread.GetUserInput(PlayerSettings.UpdateChannel ?? "stable", 40, TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            PlayerSettings.UpdateChannel = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                    };

                    internetServers.Items.Add(nameItem);
                }

                var localServs = new TabInteractiveListItem("Graphics", new List<UIMenuItem>());

                {
                    var cityDen = new UIMenuItem("City Density");
                    cityDen.SetRightLabel(GameSettings.Graphics.CityDensity.Value.ToString());
                    localServs.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.CityDensity.Value.ToString(),
                            10, TickSpinner);

                        double newSetting;
                        if (!double.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Graphics.CityDensity.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }

                {
                    var cityDen = new UIMenuItem("Depth Of Field");
                    cityDen.SetRightLabel(GameSettings.Graphics.DoF.Value.ToString());
                    localServs.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.DoF.Value.ToString(),
                            10, TickSpinner);

                        bool newSetting;
                        if (!bool.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Graphics.DoF.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }

                {
                    var cityDen = new UIMenuItem("Grass Quality");
                    cityDen.SetRightLabel(GameSettings.Graphics.GrassQuality.Value.ToString());
                    localServs.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.GrassQuality.Value.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Graphics.GrassQuality.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }

                {
                    var cityDen = new UIMenuItem("MSAA");
                    cityDen.SetRightLabel(GameSettings.Graphics.MSAA.Value.ToString());
                    localServs.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.MSAA.Value.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Graphics.MSAA.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }


                var favServers = new TabInteractiveListItem("Video", new List<UIMenuItem>());

                {
                    var cityDen = new UIMenuItem("City Density");
                    cityDen.SetRightLabel(GameSettings.Video.Windowed.Value.ToString());
                    favServers.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Video.Windowed.Value.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Video.Windowed.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }

                {
                    var cityDen = new UIMenuItem("Vertical Sync");
                    cityDen.SetRightLabel(GameSettings.Video.VSync.Value.ToString());
                    favServers.Items.Add(cityDen);

                    cityDen.Activated += (sender, item) =>
                    {
                        var strInput = InputboxThread.GetUserInput(GameSettings.Video.VSync.Value.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.SafeNotify("Input was not in the correct format.");
                            return;
                        }

                        GameSettings.Video.VSync.Value = newSetting;
                        GTANetwork.GameSettings.SaveSettings(GameSettings);
                    };
                }

                var welcomeItem = new TabSubmenuItem("settings", new List<TabItem>() { internetServers, localServs, favServers });
                MainMenu.AddTab(welcomeItem);
            }

            #endregion
            
            #region Host
            {
                #if ATTACHSERVER   
                var settingsPath = GTANInstallDir + "\\server\\settings.xml";
                var settingsFile = ServerSettings.ReadSettings(settingsPath);

                var hostStart = new TabTextItem("Start Server", "Host a Session", "Press [ENTER] to start your own server!");
                hostStart.CanBeFocused = false;

                hostStart.Activated += (sender, args) =>
                {
                    if (IsOnServer() || _serverProcess != null)
                    {
                        UI.Notify("~b~~h~GTA Network~h~~w~~n~Leave the current server first!");
                        return;
                    }

                    UI.Notify("~b~~h~GTA Network~h~~w~~n~Starting server...");
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
                        var newName = InputboxThread.GetUserInput(settingsFile.Name, 40, TickSpinner);
                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            UI.Notify("~b~~h~GTA Network~h~~w~~n~Server name must not be empty!");
                            return;
                        }
                        serverName.SetRightLabel(newName);
                        settingsFile.Name = newName;
                        ServerSettings.WriteSettings(settingsPath, settingsFile);
                    };
                }

                {
                    var serverName = new UIMenuItem("Password");
                    settingsList.Add(serverName);
                    serverName.SetRightLabel(settingsFile.Password);
                    serverName.Activated += (sender, item) =>
                    {
                        var newName = InputboxThread.GetUserInput(settingsFile.Password, 40, TickSpinner);
                        serverName.SetRightLabel(newName);
                        settingsFile.Password = newName;
                        ServerSettings.WriteSettings(settingsPath, settingsFile);
                    };
                }

                {
                    var serverName = new UIMenuItem("Player Limit");
                    settingsList.Add(serverName);
                    serverName.SetRightLabel(settingsFile.MaxPlayers.ToString());
                    serverName.Activated += (sender, item) =>
                    {
                        var newName = InputboxThread.GetUserInput(settingsFile.MaxPlayers.ToString(), 40, TickSpinner);
                        int newLimit;
                        if (string.IsNullOrWhiteSpace(newName) || !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit))
                        {
                            UI.Notify("~b~~h~GTA Network~h~~w~~n~Invalid input for player limit!");
                            return;
                        }

                        serverName.SetRightLabel(newName);
                        settingsFile.MaxPlayers = newLimit;
                        ServerSettings.WriteSettings(settingsPath, settingsFile);
                    };
                }

                {
                    var serverName = new UIMenuItem("Port");
                    settingsList.Add(serverName);
                    serverName.SetRightLabel(settingsFile.Port.ToString());
                    serverName.Activated += (sender, item) =>
                    {
                        var newName = InputboxThread.GetUserInput(settingsFile.Port.ToString(), 40, TickSpinner);
                        int newLimit;
                        if (string.IsNullOrWhiteSpace(newName) || !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit) || newLimit < 1024)
                        {
                            UI.Notify("~b~~h~GTA Network~h~~w~~n~Invalid input for server port!");
                            return;
                        }

                        serverName.SetRightLabel(newName);
                        settingsFile.Port = newLimit;
                        ServerSettings.WriteSettings(settingsPath, settingsFile);
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

                        var item = new UIMenuCheckboxItem(resourceName, settingsFile.Resources.Any(res => res.Path == resourceName));
                        resourcesList.Add(item);
                        item.CheckboxEvent += (sender, @checked) =>
                        {
                            if (@checked)
                            {
                                settingsFile.Resources.Add(new ServerSettings.SettingsResFilepath() { Path = resourceName });
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

                
                var welcomeItem = new TabSubmenuItem("host", new List<TabItem> { hostStart, serverSettings, resources });
                MainMenu.AddTab(welcomeItem);
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
                    Environment.Exit(0);
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
                Util.SafeNotify("Server added to favorites!");
                item.Activated += (faf, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers.");

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

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;

        public static List<int> EntityCleanup;
        public static List<int> BlipCleanup;
        public static Dictionary<int, MarkerProperties> _localMarkers = new Dictionary<int, MarkerProperties>();

        private int _markerCount;

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();

        private static string Weather { get; set; }
        private static TimeSpan? Time { get; set; }

        public static void AddMap(ServerMap map)
        {
            //File.WriteAllText("logs\\map.json", JsonConvert.SerializeObject(map));

            UI.ShowSubtitle("Downloading Map...", 500000);

            try
            {
                if (map.LoadedIpl != null)
                    foreach (var ipl in map.LoadedIpl)
                    {
                        Function.Call(Hash.REQUEST_IPL, ipl);
                    }

                if (map.RemovedIpl != null)
                    foreach (var ipl in map.RemovedIpl)
                    {
                        Function.Call(Hash.REMOVE_IPL, ipl);
                    }

                if (map.Objects != null)
                    foreach (var pair in map.Objects)
                    {
                        NetEntityHandler.CreateObject(pair.Key, pair.Value);
                        UI.ShowSubtitle("Creating object...", 500000);
                    }

                if (map.Vehicles != null)
                    foreach (var pair in map.Vehicles)
                    {
                        NetEntityHandler.CreateVehicle(pair.Key, pair.Value);
                        UI.ShowSubtitle("Creating vehicle...", 500000);
                    }

                if (map.Blips != null)
                {
                    foreach (var blip in map.Blips)
                    {
                        NetEntityHandler.CreateBlip(blip.Key, blip.Value);
                    }
                }

                if (map.Markers != null)
                {
                    foreach (var marker in map.Markers)
                    {
                        NetEntityHandler.CreateMarker(marker.Key, marker.Value);
                    }
                }

                if (map.Pickups != null)
                {
                    foreach (var pickup in map.Pickups)
                    {
                        NetEntityHandler.CreatePickup(pickup.Key, pickup.Value);
                    }
                }

                if (map.Players != null)
                {
                    foreach (var pair in map.Players)
                    {
                        var ourPed = NetEntityHandler.NetToEntity(pair.Key);
                        if (ourPed != null)
                        {
                            for (int i = 0; i < 15; i++)
                            {
                                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ourPed, i,
                                    pair.Value.Props.Get((byte) i),
                                    pair.Value.Textures.Get((byte) i), 2);
                            }

                            if (!Names.ContainsKey(pair.Key)) Names.Add(pair.Key, pair.Value.Name);
                            else Names[pair.Key] = pair.Value.Name;

                            ourPed.Alpha = pair.Value.Alpha;

                            if (NetEntityHandler.NetToEntity(pair.Key).Handle == Game.Player.Character.Handle)
                            {
                                // It's us!
                                var remPl = NetEntityHandler.NetToStreamedItem(pair.Key) as RemotePlayer;
                                remPl.Name = pair.Value.Name;
                            }
                            else
                            { 
                                var ourSyncPed = NetEntityHandler.GetPlayer(pair.Key);
                                if (ourSyncPed != null)
                                {
                                    NetEntityHandler.UpdatePlayer(pair.Key, pair.Value);

                                    if (ourSyncPed.Character != null)
                                    {
                                        ourSyncPed.Character.RelationshipGroup = (pair.Value.Team == LocalTeam &&
                                                                                  pair.Value.Team != -1)
                                            ? Main.FriendRelGroup
                                            : Main.RelGroup;
                                        if (ourSyncPed.Character.CurrentBlip != null)
                                        {
                                            ourSyncPed.Character.CurrentBlip.Sprite = (BlipSprite) pair.Value.BlipSprite;
                                            ourSyncPed.Character.CurrentBlip.Color = (BlipColor) pair.Value.BlipColor;
                                            ourSyncPed.Character.CurrentBlip.Alpha = pair.Value.BlipAlpha;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                UI.Notify("FATAL ERROR WHEN PARSING MAP");
                UI.Notify(ex.Message);
                Client.Disconnect("Map Parse Error");

                LogManager.LogException(ex, "MAP PARSE");

                return;
            }

            World.CurrentDayTime = new TimeSpan(map.Hours, map.Minutes, 00);
            Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, map.Weather);

            Time = new TimeSpan(map.Hours, map.Minutes, 00);
            Weather = map.Weather;

            Function.Call(Hash.PAUSE_CLOCK, true);
        }

        public static void StartClientsideScripts(ScriptCollection scripts)
        {
            if (scripts.ClientsideScripts != null)
                JavascriptHook.StartScripts(scripts);
        }

        public static Dictionary<int, int> CheckPlayerVehicleMods()
        {
            if (!Game.Player.Character.IsInVehicle()) return null;

            if (_modSwitch % 30 == 0)
            {
                var id = _modSwitch/30;
                var mod = Game.Player.Character.CurrentVehicle.GetMod((VehicleMod) id);
                if (mod != -1)
                {
                    lock (_vehMods)
                    {
                        if (!_vehMods.ContainsKey(id)) _vehMods.Add(id, mod);

                        _vehMods[id] = mod;
                    }
                }
            }

            _modSwitch++;

            if (_modSwitch >= 1500) _modSwitch = 0;

            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch % 30 == 0)
            {
                var id = _pedSwitch / 30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Game.Player.Character.Handle, id);
                if (mod != -1)
                {
                    lock (_pedClothes)
                    {
                        if (!_pedClothes.ContainsKey(id)) _pedClothes.Add(id, mod);

                        _pedClothes[id] = mod;
                    }
                }
            }

            _pedSwitch++;

            if (_pedSwitch >= 450) _pedSwitch = 0;

            return _pedClothes;
        }

        public const NetDeliveryMethod SYNC_MESSAGE_TYPE = NetDeliveryMethod.UnreliableSequenced; // unreliable_sequenced
	    private static bool _sendData = true;

        private static bool _lastPedData;
        private static int _lastLightSync;
        private static int LIGHT_SYNC_RATE = 1500;

        public static void SendPlayerData()
        {
            if (IsSpectating || !_sendData ) return; //|| !HasFinishedDownloading
            var player = Game.Player.Character;
            
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;
                
                var horn = Game.Player.IsPressingHorn;
                var siren = veh.SirenActive;
                var vehdead = veh.IsDead;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.VehicleHandle = NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.PlayerHealth = (byte)(100 * ((player.Health < 0 ? 0 : player.Health) / (float)player.MaxHealth));
                obj.VehicleHealth = veh.EngineHealth;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.RPM = veh.CurrentRPM;
                obj.VehicleSeat = (short)Util.GetPedSeat(player); 
                obj.Flag = 0;
	            obj.Steering = veh.SteeringAngle;

                if (horn)
                    obj.Flag |= (byte) VehicleDataFlags.PressingHorn;
                if (siren)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;
                if (vehdead)
                    obj.Flag |= (byte)VehicleDataFlags.VehicleDead;

                if (Util.GetResponsiblePed(veh).Handle == player.Handle)
                    obj.Flag |= (byte) VehicleDataFlags.Driver;


                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.Flag |= (byte) VehicleDataFlags.MountedWeapon;
                    obj.AimCoords = new GTANetworkShared.Vector3(0, 0, 0);
                    obj.WeaponHash = GetCurrentVehicleWeaponHash(Game.Player.Character);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.Flag |= (byte)VehicleDataFlags.MountedWeapon;

                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                }
                else
                {
                    if (player.IsSubtaskActive(200) && 
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                    if (player.IsSubtaskActive(200) && // or 290
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Aiming;
                    //obj.IsShooting = Game.Player.Character.IsShooting;
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();
                }

                Vehicle trailer;

                if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    trailer = veh.TowedVehicle;
                else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                else trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);

                if (trailer != null && trailer.Exists())
                {
                    obj.Trailer = trailer.Position.ToLVector();
                }

                //var bin = SerializeBinary(DeltaCompressor.CompressData(obj));
                var bin = PacketOptimization.WritePureSync(obj);

                var msg = Client.CreateMessage();
                msg.Write((int)PacketType.VehiclePureSync);
                msg.Write(bin.Length);
                msg.Write(bin);
                try
                {
                    Client.SendMessage(msg, SYNC_MESSAGE_TYPE, (int) ConnectionChannel.PureSync);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    LogManager.LogException(ex, "SENDPLAYERDATA");
                }

                if (_lastPedData || Environment.TickCount - _lastLightSync > LIGHT_SYNC_RATE)
                {
                    _lastLightSync = Environment.TickCount;

                    LogManager.DebugLog("SENDING LIGHT VEHICLE SYNC");

                    var lightBin = PacketOptimization.WriteLightSync(obj);

                    var lightMsg = Client.CreateMessage();
                    lightMsg.Write((int)PacketType.VehicleLightSync);
                    lightMsg.Write(lightBin.Length);
                    lightMsg.Write(lightBin);
                    try
                    {
                        Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                    }
                    catch (Exception ex)
                    {
                        Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                        LogManager.LogException(ex, "SENDPLAYERDATA");
                    }

                    _bytesSent += lightBin.Length;
                    _messagesSent++;
                }

                _lastPedData = false;

                _averagePacketSize.Add(bin.Length);
                if (_averagePacketSize.Count > 10)
                    _averagePacketSize.RemoveAt(0);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool aiming = player.IsSubtaskActive(ESubtask.AIMED_SHOOTING_ON_FOOT) || player.IsSubtaskActive(ESubtask.AIMING_THROWABLE); // Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);
                
                Vector3 aimCoord = new Vector3();
                if (aiming || shooting)
                {
                    aimCoord = RaycastEverything(new Vector2(0, 0));
                }

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Rotation.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = (byte)(100 * ((player.Health < 0 ? 0 : player.Health) / (float)player.MaxHealth));
                obj.Velocity = player.Velocity.ToLVector();

                obj.Flag = 0;

                if (player.IsRagdoll)
                    obj.Flag |= (int)PedDataFlags.Ragdoll;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                    Game.Player.Character.IsInAir)
                    obj.Flag |= (int) PedDataFlags.InFreefall;
                if (player.IsInMeleeCombat)
                    obj.Flag |= (int)PedDataFlags.InMeleeCombat;
                if (aiming)
                    obj.Flag |= (int)PedDataFlags.Aiming;
                if ((shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) && !player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack)))
                    obj.Flag |= (int)PedDataFlags.Shooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (int)PedDataFlags.Jumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (int)PedDataFlags.ParachuteOpen;
                if (player.IsInCover())
                    obj.Flag |= (int) PedDataFlags.IsInCover;
                if (!Function.Call<bool>((Hash) 0x6A03BF943D767C93, player))
                    obj.Flag |= (int) PedDataFlags.IsInLowerCover;
                if (player.IsInCoverFacingLeft)
                    obj.Flag |= (int) PedDataFlags.IsInCoverFacingLeft;
                if (player.IsReloading)
                    obj.Flag |= (int)PedDataFlags.IsReloading;

                obj.Speed = GetPedWalkingSpeed(player);

                //var bin = SerializeBinary(DeltaCompressor.CompressData(obj));
                var bin = PacketOptimization.WritePureSync(obj);

                var msg = Client.CreateMessage();

                msg.Write((int)PacketType.PedPureSync);
                msg.Write(bin.Length);
                msg.Write(bin);

                try
                {
                    Client.SendMessage(msg, SYNC_MESSAGE_TYPE, (int)ConnectionChannel.PureSync);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    LogManager.LogException(ex, "SENDPLAYERDATAPED");
                }

                LogManager.DebugLog("TIME SINCE LAST LIGHTSYNC: " + (Environment.TickCount - _lastLightSync));
                if (!_lastPedData || Environment.TickCount - _lastLightSync > LIGHT_SYNC_RATE)
                {
                    _lastLightSync = Environment.TickCount;

                    LogManager.DebugLog("SENDING LIGHT PED SYNC");

                    var lightBin = PacketOptimization.WriteLightSync(obj);

                    var lightMsg = Client.CreateMessage();
                    lightMsg.Write((int)PacketType.PedLightSync);
                    lightMsg.Write(lightBin.Length);
                    lightMsg.Write(lightBin);
                    try
                    {
                        var result = Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                        LogManager.DebugLog("LIGHT PED SYNC RESULT :" + result);
                    }
                    catch (Exception ex)
                    {
                        Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                        LogManager.LogException(ex, "SENDPLAYERDATA");
                    }

                    _bytesSent += lightBin.Length;
                    _messagesSent++;
                }

                _lastPedData = true;

                _averagePacketSize.Add(bin.Length);
                if (_averagePacketSize.Count > 10)
                    _averagePacketSize.RemoveAt(0);
                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }

        ///*
        
        /// <summary>
        /// Debug use only
        /// </summary>
        /// <returns></returns>
        public PedData PackagePedData()
        {
            var player = Game.Player.Character;

            if (player.IsInVehicle())
            {
                return null;
            }
            else
            {
                bool aiming = player.IsSubtaskActive(ESubtask.AIMED_SHOOTING_ON_FOOT); // Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                Vector3 aimCoord = new Vector3();
                if (aiming || shooting)
                {
                    aimCoord = RaycastEverything(new Vector2(0, 0));
                }

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Rotation.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = (byte)(100 * ((player.Health < 0 ? 0 : player.Health) / (float)player.MaxHealth));
                obj.Velocity = player.Velocity.ToLVector();
                obj.Flag = 0;
                obj.Speed = (byte) GetPedWalkingSpeed(player);
                obj.Latency = _debugInterval/1000f;

                if (player.IsRagdoll)
                    obj.Flag |= (int)PedDataFlags.Ragdoll;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                    Game.Player.Character.IsInAir)
                    obj.Flag |= (int)PedDataFlags.InFreefall;
                if (player.IsInMeleeCombat)
                    obj.Flag |= (int)PedDataFlags.InMeleeCombat;
                if (aiming)
                    obj.Flag |= (int)PedDataFlags.Aiming;
                if ((shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) && !player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack)))
                    obj.Flag |= (int)PedDataFlags.Shooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (int)PedDataFlags.Jumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (int)PedDataFlags.ParachuteOpen;
                if (player.IsInCover())
                    obj.Flag |= (int)PedDataFlags.IsInCover;
                if (!Function.Call<bool>((Hash)0x6A03BF943D767C93, player))
                    obj.Flag |= (int)PedDataFlags.IsInLowerCover;
                if (player.IsInCoverFacingLeft)
                    obj.Flag |= (int)PedDataFlags.IsInCoverFacingLeft;

                obj.Speed = GetPedWalkingSpeed(player);
                return obj;
            }
        }


        /// <summary>
        /// Debug use only
        /// </summary>
        /// <returns></returns>
        public VehicleData PackageVehicleData()
        {
            var player = Game.Player.Character;

            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var horn = Game.Player.IsPressingHorn;
                var siren = veh.SirenActive;
                var vehdead = veh.IsDead;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.VehicleHandle = NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.PlayerHealth = (byte)(100 * (player.Health / (float)player.MaxHealth));
                obj.VehicleHealth = veh.EngineHealth;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.RPM = veh.CurrentRPM;
                obj.VehicleSeat = (short)Util.GetPedSeat(player);
                obj.Flag = 0;
                obj.Steering = veh.SteeringAngle;
                obj.Latency = _debugInterval/1000f;

                if (horn)
                    obj.Flag |= (byte)VehicleDataFlags.PressingHorn;
                if (siren)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;
                if (vehdead)
                    obj.Flag |= (byte)VehicleDataFlags.VehicleDead;

                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.WeaponHash = GetCurrentVehicleWeaponHash(Game.Player.Character);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else
                {
                    if (player.IsSubtaskActive(200) &&
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                    if (player.IsSubtaskActive(200) && // or 290
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Aiming;
                    //obj.IsShooting = Game.Player.Character.IsShooting;
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();
                }

                Vehicle trailer;

                if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    trailer = veh.TowedVehicle;
                else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                else trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);

                if (trailer != null && trailer.Exists())
                {
                    obj.Trailer = trailer.Position.ToLVector();
                }

                return obj;
            }
            else
            {
                return null;
            }
        }
        //*/

        public static byte GetPedWalkingSpeed(Ped ped)
        {
            byte output = 0;

            if (Function.Call<bool>(Hash.IS_PED_WALKING, ped))
                output = 1;
            if (Function.Call<bool>(Hash.IS_PED_RUNNING, ped))
                output = 2;
            if (Function.Call<bool>(Hash.IS_PED_SPRINTING, ped))
                output = 3;
            if (Function.Call<bool>(Hash.IS_PED_STRAFING, ped))
                output = output; // do nothing, yet

            return output;
        }

        public static void InvokeFinishedDownload()
        {
            var confirmObj = Client.CreateMessage();
            confirmObj.Write((int)PacketType.ConnectionConfirmed);
            confirmObj.Write(true);
            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

            HasFinishedDownloading = true;
        }

        public static int GetCurrentVehicleWeaponHash(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                var outputArg = new OutputArgument();
                var success = Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, ped, outputArg);
                if (success)
                {
                    return outputArg.GetResult<int>();
                }
                else
                {
                    return 0;
                }
            }
            return 0;
        }

        private Vehicle _lastPlayerCar;
        private int _lastModel;
        private bool _whoseturnisitanyways;

        private Vector3 offset;
        private DateTime _start;

        private bool _hasInitialized;
        private bool _hasPlayerSpawned;

        private int _debugStep;

        private int DEBUG_STEP
        {
            get { return _debugStep; }
            set
            {
                _debugStep = value;
                //LogManager.DebugLog(value.ToString());
            }
        }

        private int _debugPickup;
        private int _debugmask;
        private Vehicle _debugVehicle;
        private bool _lastSpectating;
        private int _currentSpectatingPlayerIndex;
        public SyncPed CurrentSpectatingPlayer;
        private Vector3 _lastWaveReset;
        public static DateTime LastCarEnter;
        private float _debugPed;
        private Dictionary<int, int> _debugSettings = new Dictionary<int, int>(); 

        // netstats
        private int _lastBytesSent;
        private int _lastBytesReceived;
        private int _lastCheck;

        private int _bytesSentPerSecond;
        private int _bytesReceivedPerSecond;
        //

        public void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;
            var res = UIMenu.GetScreenResolutionMantainRatio();

            if (Environment.TickCount - _lastCheck > 1000)
            {
                _bytesSentPerSecond = _bytesSent - _lastBytesSent;
                _bytesReceivedPerSecond = _bytesReceived - _lastBytesReceived;

                _lastBytesReceived = _bytesReceived;
                _lastBytesSent = _bytesSent;


                _lastCheck = Environment.TickCount;
            }


            if (!_hasInitialized)
            {
                RebuildServerBrowser();
                
                _hasInitialized = true;
            }

            if (!_hasPlayerSpawned && player != null && player.Handle != 0 && !Game.IsLoading)
            {
                Game.FadeScreenOut(1);
                
                Game.Player.Character.Position = _vinewoodSign;
                Script.Wait(500);
                Util.SetPlayerSkin(PedHash.Clown01SMY);
                Game.Player.Character.SetDefaultClothes();
                MainMenu.Visible = true;
                World.RenderingCamera = MainMenuCamera;
                MainMenu.RefreshIndex();
                Game.Player.Character.MaxHealth = 200;
                Game.Player.Character.Health = 200;
                _hasPlayerSpawned = true;
                Game.FadeScreenIn(1000);
            }

            DEBUG_STEP = 0;
            Game.DisableControl(0, Control.EnterCheatCode);
            Game.DisableControl(0, Control.FrontendPauseAlternate);
            Game.DisableControl(0, Control.FrontendSocialClub);
            Game.DisableControl(0, Control.FrontendSocialClubSecondary);

            if (Game.IsControlJustPressed(0, Control.FrontendPauseAlternate) && !MainMenu.Visible && !_wasTyping)
            {
                MainMenu.Visible = true;

                if (!IsOnServer())
                {
                    if (MainMenu.Visible)
                        World.RenderingCamera = MainMenuCamera;
                    else
                        World.RenderingCamera = null;
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
                    MainMenu.ProcessControls();
                MainMenu.Update();
                MainMenu.CanLeave = IsOnServer();
                if (MainMenu.Visible && !MainMenu.TemporarilyHidden && !_mainMapItem.Focused && File.Exists(GTANInstallDir + "\\images\\scavatar.png"))
                {
                    var safe = new Point(300, 180);
                    Sprite.DrawTexture(GTANInstallDir + "\\images\\scavatar.png", new Point((int)res.Width - safe.X - 64, safe.Y - 80), new Size(64, 64));
                }
            }
            DEBUG_STEP = 1;
			
            if (_isGoingToCar && Game.IsControlJustPressed(0, Control.PhoneCancel))
            {
                Game.Player.Character.Task.ClearAll();
                _isGoingToCar = false;
            }

            DEBUG_STEP = 2;
            /*
            if (Game.Player.Character.IsInVehicle())
            {
                var pos = Game.Player.Character.CurrentVehicle.GetOffsetInWorldCoords(offset);
                UI.ShowSubtitle(offset.ToString());
                World.DrawMarker(MarkerType.DebugSphere, pos, new Vector3(), new Vector3(), new Vector3(0.2f, 0.2f, 0.2f), Color.Red);
            }*/
            /*
            if (Game.IsKeyPressed(Keys.NumPad7))
            {
                offset = new Vector3(offset.X, offset.Y, offset.Z + 0.005f);
            }

            if (Game.IsKeyPressed(Keys.NumPad1))
            {
                offset = new Vector3(offset.X, offset.Y, offset.Z - 0.005f);
            }

            if (Game.IsKeyPressed(Keys.NumPad4))
            {
                offset = new Vector3(offset.X, offset.Y - 0.005f, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad6))
            {
                offset = new Vector3(offset.X, offset.Y + 0.005f, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad2))
            {
                offset = new Vector3(offset.X - 0.005f, offset.Y, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad8))
            {
                offset = new Vector3(offset.X + 0.005f, offset.Y, offset.Z);
            }
			*/
            /*
            if (Game.IsControlJustPressed(0, Control.Context))
            {
                var p = Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0f));
                //_debugPickup = Function.Call<int>(Hash.CREATE_PICKUP, 1295434569, p.X, p.Y, p.Z, 0, 1, 1, 0);
                int mask = 0;
                mask |= 1 << _debugmask;
                //mask |= 1 << 4;
                //mask |= 1 << 8;
                //mask |= 1 << 1;
                _debugPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, 1295434569, p.X, p.Y, p.Z, 0, 0, 0, mask, 1, 2, true, 0);
            }

            if (_debugPickup != 0)
            {
                var obj = Function.Call<int>(Hash.GET_PICKUP_OBJECT, _debugPickup);
                new Prop(obj).FreezePosition = true;
                var exist = Function.Call<bool>(Hash.DOES_PICKUP_EXIST, _debugPickup);
                UI.ShowSubtitle(_debugPickup + " (exists? " + exist + ") picked up obj (" + obj + "): " + Function.Call<bool>(Hash.HAS_PICKUP_BEEN_COLLECTED, obj));
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind) && _debugPickup != 0)
            {
                Function.Call(Hash.REMOVE_PICKUP, _debugPickup);
            }

            if (Game.IsControlJustPressed(0, Control.VehicleDuck))
            {
                _debugmask++;
                UI.Notify("new bit pos: " + _debugmask);
            }

            UI.ShowSubtitle(Game.Player.Character.Weapons.Current.Hash.ToString());
            new UIResText(Game.Player.Character.Health + "/" + Game.Player.Character.MaxHealth, new Point(), 0.5f).Draw();

            
            for (int i = 0; i < 2000; i++)
            {
                var val = Function.Call<int>(Hash.GET_PROFILE_SETTING, i);
                if (_debugSettings.ContainsKey(i))
                {
                    if (_debugSettings[i] != val)
                    {
                        UI.Notify("SETTINGS ID " + i + " CHANGED TO " + val);
                    }
                    _debugSettings[i] = val;
                }
                else
                {
                    _debugSettings.Add(i, val);
                }
            }


            */

            DEBUG_STEP = 3;
#if DEBUG
            /*
            var outArg = new OutputArgument();
            if (Game.IsControlJustPressed(0, Control.Context))
            {
                var pos = Game.Player.Character.Position;
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "scr_rcbarry2");
                Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "scr_rcbarry2");
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, "scr_clown_appears", pos.X, pos.Y, pos.Z, 0, 0, 0, 2f, 0, 0, 0);
            }
            
            if (player.IsInVehicle()) UI.ShowSubtitle(""+ player.CurrentVehicle.Velocity);
            else UI.ShowSubtitle(""+ player.Velocity);

            */
            if (display)
            {
                Debug();
                //unsafe
                //{
                    //UI.ShowSubtitle(new IntPtr(Game.Player.Character.MemoryAddress).ToInt64().ToString("X"));
                //}
                //Game.Player.Character.Task.AimAt(Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5f, 0)), -1);
            }
            DEBUG_STEP = 4;
            if (_debugWindow)
            {
                _debug.Visible = true;
                _debug.Draw();
            }

            /*
            UI.ShowSubtitle(Game.Player.Character.RelationshipGroup.ToString());
            if (Game.Player.Character.LastVehicle != null)
            {
                unsafe
                {
                    var address = new IntPtr(Game.Player.Character.LastVehicle.MemoryAddress);
                    UI.ShowSubtitle(address + " (" + address.ToInt64() + ")\n" + Game.Player.Character.LastVehicle.SteeringScale);
                }
            }

            if (Game.IsControlPressed(0, Control.LookBehind))
            {
                Game.Player.Character.LastVehicle.SteeringAngle = -0.69f;
            }

			var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Game.Player.Character);

	        if (gunEnt != null)
	        {
				var start = gunEnt.GetOffsetInWorldCoords(offset);
				World.DrawMarker(MarkerType.DebugSphere, start, new Vector3(), new Vector3(), new Vector3(0.01f, 0.01f, 0.01f), Color.Red);
				UI.ShowSubtitle(offset.ToString());
				if (Game.IsKeyPressed(Keys.NumPad3))
		        {
			        var end = Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5f, 0f));

			        Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
				        end.X,
				        end.Y, end.Z, 100, true, (int) WeaponHash.APPistol, Game.Player.Character, true, false, 100);
					Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
			        UI.ShowSubtitle("Bullet!");
		        }
	        }*/


            /*
            if (Game.IsControlJustPressed(0, Control.Context))
            {
                //Game.Player.Character.Task.ShootAt(World.GetCrosshairCoordinates().HitCoords, -1);
                var k = World.GetCrosshairCoordinates().HitCoords;
                //Function.Call(Hash.ADD_VEHICLE_SUBTASK_ATTACK_COORD, Game.Player.Character, k.X, k.Y, k.Z);
                _lastModel =
                    World.CreateRandomPed(Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0))).Handle;
                new Ped(_lastModel).Weapons.Give(WeaponHash.MicroSMG, 500, true, true);
                _tmpCar = World.CreateVehicle(new Model(VehicleHash.Adder),
                    Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0)), 0f);
                new Ped(_lastModel).SetIntoVehicle(_tmpCar, VehicleSeat.Passenger);
                Function.Call(Hash.TASK_DRIVE_BY, _lastModel, 0, 0, k.X, k.Y, k.Z, 0, 0, 0, unchecked ((int) FiringPattern.FullAuto));
            }

            if (Game.IsKeyPressed(Keys.D5))
            {
                new Ped(_lastModel).Task.ClearAll();
            }

            if (Game.IsKeyPressed(Keys.D6))
            {
                var k = World.GetCrosshairCoordinates().HitCoords;
                Function.Call(Hash.TASK_DRIVE_BY, _lastModel, 0, 0, k.X, k.Y, k.Z, 0, 0, 0, unchecked((int)FiringPattern.FullAuto));
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                Game.Player.Character.Task.ClearAll();
                new Ped(_lastModel).Delete();
                _tmpCar.Delete();
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                var mod = new Model(PedHash.Zombie01);
                mod.Request(10000);
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, mod.Hash);
            }
            */
            /*
            if (Game.IsControlPressed(0, Control.LookBehind) && Game.Player.Character.IsInVehicle())
            {
                Game.Player.Character.CurrentVehicle.CurrentRPM = 1f;
                Game.Player.Character.CurrentVehicle.Acceleration = 1f;
            }

            if (Game.Player.Character.IsInVehicle())
            {
                UI.ShowSubtitle("RPM: " + Game.Player.Character.CurrentVehicle.CurrentRPM + " AC: " + Game.Player.Character.CurrentVehicle.Acceleration);
            }*/
#endif
            DEBUG_STEP = 5;

            DEBUG_STEP = 6;

            if (Client == null || Client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                Client.ConnectionStatus == NetConnectionStatus.None) return;
            _verionLabel.Position = new Point((int) (res.Width/2), 0);
            _verionLabel.TextAlignment = UIResText.Alignment.Centered;
            _verionLabel.Draw();
            DEBUG_STEP = 7;
            if (_wasTyping)
                Game.DisableControl(0, Control.FrontendPauseAlternate);
            DEBUG_STEP = 8;
            var playerCar = Game.Player.Character.CurrentVehicle;
            DEBUG_STEP = 9;
            Watcher.Tick();
            DEBUG_STEP = 10;

            if (playerCar != _lastPlayerCar)
            {
                if (_lastPlayerCar != null) _lastPlayerCar.IsInvincible = true;
                if (playerCar != null)
                {
                    LastCarEnter = DateTime.Now;
                    playerCar.IsInvincible = false;
                    if (!NetEntityHandler.ContainsLocalHandle(playerCar.Handle))
                    {
                        playerCar.Delete();
                        playerCar = null;
                    }
                }
            }

            if (playerCar != null)
            {
                if (Util.GetResponsiblePed(playerCar).Handle == player.Handle)
                {
                    playerCar.IsInvincible = false;
                }
                else
                {
                    playerCar.IsInvincible = true;
                }
            }

            Game.Player.Character.MaxHealth = 200;

            DEBUG_STEP = 11;
            _lastPlayerCar = playerCar;


            if (Game.IsControlJustPressed(0, Control.ThrowGrenade) && !Game.Player.Character.IsInVehicle() && IsOnServer() && !Chat.IsFocused)
            {
                var vehs = World.GetAllVehicles().OrderBy(v => (v.Position - Game.Player.Character.Position).Length()).Take(1).ToList();
                if (vehs.Any() && Game.Player.Character.IsInRangeOf(vehs[0].Position, 6f))
                {
                    var relPos = vehs[0].GetOffsetFromWorldCoords(Game.Player.Character.Position);
                    VehicleSeat seat = VehicleSeat.Any;

                    if (relPos.X < 0 && relPos.Y > 0)
                    {
                        seat = VehicleSeat.RightFront;
                    }
                    else if (relPos.X >= 0 && relPos.Y > 0)
                    {
                        seat = VehicleSeat.RightFront;
                    }
                    else if (relPos.X < 0 && relPos.Y <= 0)
                    {
                        seat = VehicleSeat.LeftRear;
                    }
                    else if (relPos.X >= 0 && relPos.Y <= 0)
                    {
                        seat = VehicleSeat.RightRear;
                    }

                    if (vehs[0].PassengerSeats == 1) seat = VehicleSeat.Passenger;

                    if (vehs[0].PassengerSeats > 3 && vehs[0].GetPedOnSeat(seat).Handle != 0)
                    {
                        if (seat == VehicleSeat.LeftRear)
                        {
                            for (int i = 3; i < vehs[0].PassengerSeats; i += 2)
                            {
                                if (vehs[0].GetPedOnSeat((VehicleSeat)i).Handle == 0)
                                {
                                    seat = (VehicleSeat)i;
                                    break;
                                }
                            }
                        }
                        else if (seat == VehicleSeat.RightRear)
                        {
                            for (int i = 4; i < vehs[0].PassengerSeats; i += 2)
                            {
                                if (vehs[0].GetPedOnSeat((VehicleSeat)i).Handle == 0)
                                {
                                    seat = (VehicleSeat)i;
                                    break;
                                }
                            }
                        }
                    }

                    if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)vehs[0].Model.Hash, 0, true) && Game.Player.Character.IsIdle && !Game.Player.IsAiming)
                        Game.Player.Character.SetIntoVehicle(vehs[0], seat);
                    else
                        Game.Player.Character.Task.EnterVehicle(vehs[0], seat, -1, 2f);
                    _isGoingToCar = true;
                }
            }

            Game.DisableControl(0, Control.SpecialAbility);
            Game.DisableControl(0, Control.SpecialAbilityPC);
            Game.DisableControl(0, Control.SpecialAbilitySecondary);
            Game.DisableControl(0, Control.CharacterWheel);
            Game.DisableControl(0, Control.Phone);


            _statsItem.Text =
                string.Format(
                    "~h~Bytes Sent~h~: {0}~n~~h~Bytes Received~h~: {1}~n~~h~Bytes Sent / Second~h~: {5}~n~~h~Bytes Received / Second~h~: {6}~n~~h~Average Packet Size~h~: {4}~n~~n~~h~Messages Sent~h~: {2}~n~~h~Messages Received~h~: {3}",
                    _bytesSent, _bytesReceived, _messagesSent, _messagesReceived,
                    _averagePacketSize.Count > 0 ? _averagePacketSize.Average() : 0, _bytesSentPerSecond,
                    _bytesReceivedPerSecond);


            DEBUG_STEP = 12;
            if (Game.IsControlPressed(0, Control.Aim) && !Game.Player.Character.IsInVehicle() &&
                Game.Player.Character.Weapons.Current.Hash != WeaponHash.Unarmed)
            {
                Game.DisableControl(0, Control.Jump);
            }
            DEBUG_STEP = 13;
            Function.Call((Hash)0x5DB660B38DD98A31, Game.Player, 0f);
            DEBUG_STEP = 14;
            Game.MaxWantedLevel = 0;
            Game.Player.WantedLevel = 0;
            DEBUG_STEP = 15;
            lock (_localMarkers)
            {
                foreach (var marker in _localMarkers)
                {
                    World.DrawMarker((MarkerType)marker.Value.MarkerType, marker.Value.Position.ToVector(),
                        marker.Value.Direction.ToVector(), marker.Value.Rotation.ToVector(),
                        marker.Value.Scale.ToVector(),
                        Color.FromArgb(marker.Value.Alpha, marker.Value.Red, marker.Value.Green, marker.Value.Blue));
                }
            }

            NetEntityHandler.DrawMarkers();
            DEBUG_STEP = 16;
            var hasRespawned = (Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) < 8000 &&
                                Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) != -1 &&
                                Game.Player.CanControlCharacter);

            if (hasRespawned && !_lastDead)
            {
                _lastDead = true;
                var msg = Client.CreateMessage();
                msg.Write((int)PacketType.PlayerRespawned);
                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                if (Weather != null) Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Weather);
                if (Time.HasValue)
                {
                    World.CurrentDayTime = new TimeSpan(Time.Value.Hours, Time.Value.Minutes, 00);
                }

                Function.Call(Hash.PAUSE_CLOCK, true);
            }
            DEBUG_STEP = 17;
            _lastDead = hasRespawned;
            DEBUG_STEP = 18;
            var killed = Game.Player.Character.IsDead;
            DEBUG_STEP = 19;
            if (killed && !_lastKilled)
            {

                var msg = Client.CreateMessage();
                msg.Write((int)PacketType.PlayerKilled);
                var killer = Function.Call<int>(Hash._GET_PED_KILLER, Game.Player.Character);
                var weapon = Function.Call<int>(Hash.GET_PED_CAUSE_OF_DEATH, Game.Player.Character);


                var killerEnt = NetEntityHandler.EntityToNet(killer);
                msg.Write(killerEnt);
                msg.Write(weapon);
                /*
                var playerMod = (PedHash)Game.Player.Character.Model.Hash;
                if (playerMod != PedHash.Michael && playerMod != PedHash.Franklin && playerMod != PedHash.Trevor)
                {
                    _lastModel = Game.Player.Character.Model.Hash;
                    var lastMod = new Model(PedHash.Michael);
                    lastMod.Request(10000);
                    Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, lastMod);
                    Game.Player.Character.Kill();
                }
                else
                {
                    _lastModel = 0;
                }
                */
                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                NativeUI.BigMessageThread.MessageInstance.ShowColoredShard("WASTED", "", HudColor.HUD_COLOUR_BLACK, HudColor.HUD_COLOUR_RED, 7000);
                Function.Call(Hash.REQUEST_SCRIPT_AUDIO_BANK, "HUD_MINI_GAME_SOUNDSET", true);
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
            }
            DEBUG_STEP = 20;
            /*
            if (Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) < 8000 &&
                Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) != -1)
            {
                if (_lastModel != 0 && Game.Player.Character.Model.Hash != _lastModel)
                {
                    var lastMod = new Model(_lastModel);
                    lastMod.Request(10000);
                    Function.Call(Hash.SET_PLAYER_MODEL, new InputArgument(Game.Player), lastMod.Hash);
                }
            }
            */

            
            DEBUG_STEP = 21;

            if (IsSpectating && !_lastSpectating)
            {
                Game.Player.Character.Alpha = 0;
                Game.Player.Character.FreezePosition = true;
                Game.Player.IsInvincible = true;
                Game.Player.Character.HasCollision = false;
            }

            else if (!IsSpectating && _lastSpectating)
            {
                Game.Player.Character.Alpha = 255;
                Game.Player.Character.FreezePosition = false;
                Game.Player.IsInvincible = false;
                Game.Player.Character.HasCollision = true;
                SpectatingEntity = 0;
                CurrentSpectatingPlayer = null;
                _currentSpectatingPlayerIndex = 0;
            }

            if (IsSpectating && SpectatingEntity != 0)
            {
                Game.Player.Character.Alpha = 0;
                Game.Player.Character.FreezePosition = true;
                Game.Player.IsInvincible = true;
                Game.Player.Character.HasCollision = false;

                Game.Player.Character.PositionNoOffset = new Prop(SpectatingEntity).Position;
            }
            else if (IsSpectating && SpectatingEntity == 0 && CurrentSpectatingPlayer == null && NetEntityHandler.ClientMap.Count(op => op is SyncPed && ((SyncPed) op).Character != null) > 0)
            {
                CurrentSpectatingPlayer = NetEntityHandler.ClientMap.Where(op => op is SyncPed && ((SyncPed)op).Character != null).ElementAt(_currentSpectatingPlayerIndex % NetEntityHandler.ClientMap.Count(op => op is SyncPed && ((SyncPed)op).Character != null)) as SyncPed;
            }
            else if (IsSpectating && SpectatingEntity == 0 && CurrentSpectatingPlayer != null)
            {
                Game.Player.Character.PositionNoOffset = CurrentSpectatingPlayer.Character.Position;
                Game.Player.Character.Alpha = 0;
                Game.Player.Character.FreezePosition = true;
                Game.Player.IsInvincible = true;
                Game.Player.Character.HasCollision = false;

                if (Game.IsControlJustPressed(0, Control.PhoneLeft))
                {
                    _currentSpectatingPlayerIndex--;
                    CurrentSpectatingPlayer = null;
                }
                else if (Game.IsControlJustPressed(0, Control.PhoneRight))
                {
                    _currentSpectatingPlayerIndex++;
                    CurrentSpectatingPlayer = null;
                }
            }

            _lastSpectating = IsSpectating;

            _lastKilled = killed;

            Function.Call(Hash.SET_RANDOM_TRAINS, 0);
            DEBUG_STEP = 22;
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, -1);
            Function.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);
            Function.Call(Hash.SET_FAR_DRAW_VEHICLES, false);
            Function.Call(Hash.DESTROY_MOBILE_PHONE);
            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
            Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Game.Player.Character, true, true);

            if ((Game.Player.Character.Position - _lastWaveReset).LengthSquared() > 100000f) // 100f * 100f
            {
                Function.Call((Hash)0x5E5E99285AE812DB);
                Function.Call((Hash)0xB96B00E976BE977F, 0f);

                _lastWaveReset = Game.Player.Character.Position;
            }

            Function.Call((Hash) 0x2F9A292AD0A3BD89);
            Function.Call((Hash) 0x5F3B7749C112D552);

            Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);

            if (Function.Call<bool>(Hash.IS_STUNT_JUMP_IN_PROGRESS))
                Function.Call(Hash.CANCEL_STUNT_JUMP);

            DEBUG_STEP = 23;
            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character) == 2)
            {
                Game.DisableControl(0, Control.Aim);
                Game.DisableControl(0, Control.Attack);
            }
            DEBUG_STEP = 24;
            if (RemoveGameEntities)
            {
                if (_whoseturnisitanyways)
                { 
                    foreach (var entity in World.GetAllPeds())
                    {
                        if (!NetEntityHandler.ContainsLocalHandle(entity.Handle))
                            entity.Delete();
                    }
                }
                else
                {
                    foreach (var entity in World.GetAllVehicles())
                    {
                        if (entity == null) continue;
                        var veh = NetEntityHandler.NetToStreamedItem(entity.Handle, useGameHandle: true) as RemoteVehicle;
                        if (veh == null)
                        {
                            entity.Delete();
                            continue;
                        }
                        veh.Position = entity.Position.ToLVector();
                        veh.Rotation = entity.Rotation.ToLVector();
                    }
                }
            }
            DEBUG_STEP = 25;
            _whoseturnisitanyways = !_whoseturnisitanyways;

            /*string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                _bytesSent / 1000, _messagesReceived, _messagesSent);
                */
            //UI.ShowSubtitle(stats);

            if (!Multithreading)
                PedThread.OnTick("thisaintnullnigga", e);

            lock (_threadJumping)
            {
                if (_threadJumping.Any())
                {
                    Action action = _threadJumping.Dequeue();
                    if (action != null) action.Invoke();
                }
            }
            DEBUG_STEP = 28;
        }

        public static bool IsOnServer()
        {
            return Client != null && Client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            Chat.OnKeyDown(e.KeyCode);
            if (e.KeyCode == Keys.F10 && !Chat.IsFocused)
            {
                MainMenu.Visible = !MainMenu.Visible;

                if (!IsOnServer())
                {
                    if (MainMenu.Visible)
                        World.RenderingCamera = MainMenuCamera;
                    else
                        World.RenderingCamera = null;
                }
                else if (MainMenu.Visible)
                {
                    RebuildPlayersList();
                }

                MainMenu.RefreshIndex();
            }

            if (e.KeyCode == Keys.F7)
            {
                ChatVisible = !ChatVisible;
            }
            
            if (e.KeyCode == PlayerSettings.ScreenshotKey && IsOnServer())
            {
                Screenshot.TakeScreenshot();
            }

            if (e.KeyCode == Keys.T && IsOnServer() && ChatVisible)
            {
                if (!_oldChat)
                {
                    Chat.IsFocused = true;
                    _wasTyping = true;
                }
                else
                {
                    var message = Game.GetUserInput(255);
                    if (!string.IsNullOrEmpty(message))
                    {
                        var obj = new ChatData()
                        {
                            Message = message,
                        };
                        var data = SerializeBinary(obj);

                        var msg = Client.CreateMessage();
                        msg.Write((int)PacketType.ChatData);
                        msg.Write(data.Length);
                        msg.Write(data);
                        Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                    }
                }
            }
        }

        public void ConnectToServer(string ip, int port = 0)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            Chat.Init();

            if (Client == null)
            {
                var cport = GetOpenUdpPort();
                if (cport == 0)
                {
                    Util.SafeNotify("No available UDP port was found.");
                    return;
                }
                _config.Port = cport;
                Client = new NetClient(_config);
                Client.Start();
            }

            lock (Npcs) Npcs = new Dictionary<string, SyncPed>();
            lock (_tickNatives) _tickNatives = new Dictionary<string, NativeData>();

            var msg = Client.CreateMessage();

            var obj = new ConnectionRequest();
            obj.SocialClubName = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Unknown" : Game.Player.Name; // To be used as identifiers in server files
            obj.DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.DisplayName) ? obj.SocialClubName : PlayerSettings.DisplayName.Trim();
            if (!string.IsNullOrEmpty(_password)) obj.Password = _password;
            obj.ScriptVersion = CurrentVersion.ToLong();
            obj.GameVersion = (byte)Game.Version;

            var bin = SerializeBinary(obj);

            msg.Write((int)PacketType.ConnectionRequest);
            msg.Write(bin.Length);
            msg.Write(bin);

            Client.Connect(ip, port == 0 ? Port : port, msg);

            var pos = Game.Player.Character.Position;
            Function.Call(Hash.CLEAR_AREA_OF_PEDS, pos.X, pos.Y, pos.Z, 100f, 0);
            Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 100f, 0);

            Function.Call(Hash.SET_GARBAGE_TRUCKS, 0);
            Function.Call(Hash.SET_RANDOM_BOATS, 0);
            Function.Call(Hash.SET_RANDOM_TRAINS, 0);

            TerminateGameScripts();
            
            _currentServerIp = ip;
            _currentServerPort = port == 0 ? Port : port;
        }

        public bool IsMessageTypeThreadsafe(NetIncomingMessageType msgType)
        {
            if (msgType == NetIncomingMessageType.StatusChanged ||
                msgType == NetIncomingMessageType.Data) return false;
            //return true;
            return false;
        }

        private bool IsPacketTypeThreadsafe(PacketType type)
        {
            if (type == PacketType.CreateEntity ||
                type == PacketType.DeleteEntity ||
                type == PacketType.FileTransferTick || // TODO: Make this threadsafe (remove UI.ShowSubtitle)
                type == PacketType.FileTransferComplete || 
                type == PacketType.ServerEvent ||
                type == PacketType.SyncEvent ||
                type == PacketType.NativeCall ||
                type == PacketType.NativeResponse)
                return false;
            //return true;
            return false;
        }

        private void ProcessDataMessage(NetIncomingMessage msg, PacketType type)
        {
            #region Data
            LogManager.DebugLog("RECEIVED DATATYPE " + type);
            switch (type)
            {
                case PacketType.VehiclePureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPureVehicleSync(data);
                        HandleVehiclePacket(packet);
                    }
                    break;
                case PacketType.VehicleLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightVehicleSync(data);
                        LogManager.DebugLog("-----RECEIVED LIGHT VEHICLE PACKET");
                        HandleVehiclePacket(packet);
                    }
                    break;
                case PacketType.PedPureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPurePedSync(data);
                        HandlePedPacket(packet);
                    }
                    break;
                case PacketType.PedLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightPedSync(data);
                        HandlePedPacket(packet);
                    }
                    break;
                case PacketType.BasicSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        int nethandle;
                        GTANetworkShared.Vector3 position;
                        PacketOptimization.ReadBasicSync(data, out nethandle, out position);
                        
                        HandleBasicPacket(nethandle, position.ToVector());
                    }
                    break;
                case PacketType.NpcVehPositionData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                        if (data == null) return;
                        /*
                        lock (Npcs)
                        {
                            if (!Npcs.ContainsKey(data.Name))
                            {
                                var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                    //data.Quaternion.ToQuaternion(), false);
                                    data.Quaternion.ToVector(), false);
                                Npcs.Add(data.Name, repr);
                                Npcs[data.Name].Name = "";
                                Npcs[data.Name].Host = data.Id;
                            }
                            if (Npcs[data.Name].Character != null)
                                NetEntityHandler.SetEntity(data.NetHandle, Npcs[data.Name].Character.Handle);

                            Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                            Npcs[data.Name].VehiclePosition =
                                data.Position.ToVector();
                            Npcs[data.Name].ModelHash = data.PedModelHash;
                            Npcs[data.Name].VehicleHash =
                                data.VehicleModelHash;
                            Npcs[data.Name].VehicleRotation =
                                data.Quaternion.ToVector();
                            //data.Quaternion.ToQuaternion();
                            Npcs[data.Name].PedHealth = data.PlayerHealth;
                            Npcs[data.Name].VehicleHealth = data.VehicleHealth;
                            //Npcs[data.Name].VehiclePrimaryColor = data.PrimaryColor;
                            //Npcs[data.Name].VehicleSecondaryColor = data.SecondaryColor;
                            Npcs[data.Name].VehicleSeat = data.VehicleSeat;
                            Npcs[data.Name].IsInVehicle = true;

                            Npcs[data.Name].IsHornPressed = data.IsPressingHorn;
                            Npcs[data.Name].Speed = data.Speed;
                            Npcs[data.Name].Siren = data.IsSirenActive;
                        }*/
                    }
                    break;
                case PacketType.NpcPedPositionData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                        if (data == null) return;
                        /*
                        lock (Npcs)
                        {
                            if (!Npcs.ContainsKey(data.Name))
                            {
                                var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                    //data.Quaternion.ToQuaternion(), false);
                                    data.Quaternion.ToVector(), false);
                                Npcs.Add(data.Name, repr);
                                Npcs[data.Name].Name = "";
                                Npcs[data.Name].Host = data.Id;
                            }
                            if (Npcs[data.Name].Character != null)
                                NetEntityHandler.SetEntity(data.NetHandle, Npcs[data.Name].Character.Handle);

                            Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                            Npcs[data.Name].Position = data.Position.ToVector();
                            Npcs[data.Name].ModelHash = data.PedModelHash;
                            //Npcs[data.Name].Rotation = data.Quaternion.ToVector();
                            Npcs[data.Name].Rotation = data.Quaternion.ToVector();
                            Npcs[data.Name].PedHealth = data.PlayerHealth;
                            Npcs[data.Name].IsInVehicle = false;
                            Npcs[data.Name].AimCoords = data.AimCoords.ToVector();
                            Npcs[data.Name].CurrentWeapon = data.WeaponHash;
                            Npcs[data.Name].IsAiming = data.IsAiming;
                            Npcs[data.Name].IsJumping = data.IsJumping;
                            Npcs[data.Name].IsShooting = data.IsShooting;
                            Npcs[data.Name].IsParachuteOpen = data.IsParachuteOpen;
                        }*/
                    }
                    break;
                case PacketType.CreateEntity:
                    {
                        var len = msg.ReadInt32();
                        LogManager.DebugLog("Received CreateEntity");
                        var data = DeserializeBinary<CreateEntity>(msg.ReadBytes(len)) as CreateEntity;
                        if (data != null && data.Properties != null)
                        {
                            LogManager.DebugLog("CreateEntity was not null. Type: " + data.EntityType);
                            LogManager.DebugLog("Model: " + data.Properties.ModelHash);
                            if (data.EntityType == (byte) EntityType.Vehicle)
                            {
                                var prop = (VehicleProperties) data.Properties;
                                var veh = NetEntityHandler.CreateVehicle(data.NetHandle, prop);
                                if (NetEntityHandler.Count(typeof(RemoteVehicle)) < StreamerThread.MAX_VEHICLES)
                                    NetEntityHandler.StreamIn(veh);
                                LogManager.DebugLog("CreateEntity done");
                            }
                            else if (data.EntityType == (byte) EntityType.Prop)
                            {
                                LogManager.DebugLog("It was a prop. Spawning...");
                                var prop = NetEntityHandler.CreateObject(data.NetHandle, data.Properties);
                                if (NetEntityHandler.Count(typeof(RemoteProp)) < StreamerThread.MAX_OBJECTS)
                                    NetEntityHandler.StreamIn(prop);
                            }
                            else if (data.EntityType == (byte) EntityType.Blip)
                            {
                                var prop = (BlipProperties) data.Properties;
                                var blip = NetEntityHandler.CreateBlip(data.NetHandle, prop);
                                if (NetEntityHandler.Count(typeof(RemoteBlip)) < StreamerThread.MAX_BLIPS)
                                    NetEntityHandler.StreamIn(blip);
                            }
                            else if (data.EntityType == (byte) EntityType.Marker)
                            {
                                var prop = (MarkerProperties) data.Properties;
                                var mark = NetEntityHandler.CreateMarker(data.NetHandle, prop);
                                NetEntityHandler.StreamIn(mark);
                            }
                            else if (data.EntityType == (byte) EntityType.Pickup)
                            {
                                var prop = (PickupProperties) data.Properties;
                                var pickup = NetEntityHandler.CreatePickup(data.NetHandle, prop);
                                if (NetEntityHandler.Count(typeof(RemotePickup)) < StreamerThread.MAX_PICKUPS)
                                    NetEntityHandler.StreamIn(pickup);
                            }
                        }
                    }
                    break;
                case PacketType.UpdateEntityProperties:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                        if (data != null && data.Properties != null)
                        {
                            switch ((EntityType) data.EntityType)
                            {
                                case EntityType.Blip:
                                    NetEntityHandler.UpdateBlip(data.NetHandle, data.Properties as Delta_BlipProperties);
                                    break;
                                case EntityType.Marker:
                                    NetEntityHandler.UpdateMarker(data.NetHandle, data.Properties as Delta_MarkerProperties);
                                    break;
                                case EntityType.Ped:
                                    NetEntityHandler.UpdatePlayer(data.NetHandle, data.Properties as Delta_PedProperties);
                                    break;
                                case EntityType.Pickup:
                                    NetEntityHandler.UpdatePickup(data.NetHandle, data.Properties as Delta_PickupProperties);
                                    break;
                                case EntityType.Prop:
                                    NetEntityHandler.UpdateProp(data.NetHandle, data.Properties as Delta_EntityProperties);
                                    break;
                                case EntityType.Vehicle:
                                    NetEntityHandler.UpdateVehicle(data.NetHandle, data.Properties as Delta_VehicleProperties);
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.DeleteEntity:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<DeleteEntity>(msg.ReadBytes(len)) as DeleteEntity;
                        if (data != null)
                        {
                            LogManager.DebugLog("RECEIVED DELETE ENTITY " + data.NetHandle);
                            var streamItem = NetEntityHandler.NetToStreamedItem(data.NetHandle);
                            if (streamItem != null)
                            {
                                NetEntityHandler.StreamOut(streamItem);
                                NetEntityHandler.RemoveByNetHandle(data.NetHandle);
                            }
                        }
                    }
                    break;
                case PacketType.StopResource:
                    {
                        var resourceName = msg.ReadString();
                        JavascriptHook.StopScript(resourceName);
                    }
                    break;
                case PacketType.FileTransferRequest:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<DataDownloadStart>(msg.ReadBytes(len)) as DataDownloadStart;
                        if (data != null)
                        {
                            var acceptDownload = DownloadManager.StartDownload(data.Id,
                                data.ResourceParent + Path.DirectorySeparatorChar + data.FileName,
                                (FileType)data.FileType, data.Length, data.Md5Hash, data.ResourceParent);
                            LogManager.DebugLog("FILE TYPE: " + (FileType) data.FileType);
                            LogManager.DebugLog("DOWNLOAD ACCEPTED: " + acceptDownload);
                            var newMsg = Client.CreateMessage();
                            newMsg.Write((int)PacketType.FileAcceptDeny);
                            newMsg.Write(data.Id);
                            newMsg.Write(acceptDownload);
                            Client.SendMessage(newMsg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                        }
                        else
                        {
                            LogManager.DebugLog("DATA WAS NULL ON REQUEST");
                        }
                    }
                    break;
                case PacketType.FileTransferTick:
                    {
                        var channel = msg.ReadInt32();
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        DownloadManager.DownloadPart(channel, data);
                    }
                    break;
                case PacketType.FileTransferComplete:
                    {
                        var id = msg.ReadInt32();
                        DownloadManager.End(id);
                    }
                    break;
                case PacketType.ChatData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                        if (data != null && !string.IsNullOrEmpty(data.Message))
                        {
                            Chat.AddMessage(data.Sender, data.Message);
                        }
                    }
                    break;
                case PacketType.ServerEvent:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                        if (data != null)
                        {
                            var args = DecodeArgumentListPure(data.Arguments?.ToArray() ?? new NativeArgument[0]).ToList();
                            switch ((ServerEventType)data.EventType)
                            {
                                case ServerEventType.PlayerSpectatorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var spectating = (bool)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.IsSpectating = spectating;
                                                if (spectating)
                                                    pair.Clear();
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            IsSpectating = spectating;
                                            if (spectating && args.Count >= 3)
                                            {
                                                var target = (int)args[2];
                                                var targetHandle = NetEntityHandler.NetToEntity(target);

                                                var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                                if (pair != null)
                                                {
                                                    pair.Clear();
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipColorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newColor = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.BlipColor = newColor;
                                                if (pair.Character != null &&
                                                    pair.Character.CurrentBlip != null)
                                                {
                                                    pair.Character.CurrentBlip.Color = (BlipColor)newColor;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipSpriteChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newSprite = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.BlipSprite = newSprite;
                                                if (pair.Character != null && pair.Character.CurrentBlip != null)
                                                    pair.Character.CurrentBlip.Sprite =
                                                        (BlipSprite)newSprite;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipAlphaChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newAlpha = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.BlipAlpha = (byte)newAlpha;
                                                if (pair.Character != null &&
                                                    pair.Character.CurrentBlip != null)
                                                    pair.Character.CurrentBlip.Alpha = newAlpha;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerTeamChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newTeam = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.Team = newTeam;
                                                if (pair.Character != null)
                                                    pair.Character.RelationshipGroup = (newTeam == LocalTeam &&
                                                                                                newTeam != -1)
                                                        ? Main.FriendRelGroup
                                                        : Main.RelGroup;
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            LocalTeam = newTeam;
                                            foreach (var opponent in NetEntityHandler.ClientMap.Where(item => item is SyncPed && ((SyncPed)item).LocalHandle != -2).Cast<SyncPed>())
                                            {
                                                if (opponent.Character != null &&
                                                    (opponent.Team == newTeam && newTeam != -1))
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.FriendRelGroup;
                                                }
                                                else
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.RelGroup;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStart:
                                    {
                                        var netHandle = (int)args[0];
                                        var animFlag = (int)args[1];
                                        var animDict = (string)args[2];
                                        var animName = (string)args[3];

                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null && pair.Character != null && pair.Character.Exists())
                                            {
                                                pair.IsCustomAnimationPlaying = true;
                                                pair.CustomAnimationName = animName;
                                                pair.CustomAnimationDictionary = animDict;
                                                pair.CustomAnimationFlag = animFlag;

                                                if (!string.IsNullOrEmpty(animName) &&
                                                    string.IsNullOrEmpty(animDict))
                                                {
                                                    pair.IsCustomScenarioPlaying = true;
                                                    pair.HasCustomScenarioStarted = false;
                                                }
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            if (string.IsNullOrEmpty(animDict))
                                            {
                                                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, Game.Player.Character, animName, 0, 0);
                                            }
                                            else
                                            {
                                                Function.Call(Hash.TASK_PLAY_ANIM, Game.Player.Character,
                                                    Util.LoadDict(animDict), animName, 8f, 10f, -1, animFlag, -8f, 1, 1, 1);
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStop:
                                    {
                                        var netHandle = (int)args[0];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.IsCustomAnimationPlaying = false;
                                                pair.CustomAnimationName = null;
                                                pair.CustomAnimationDictionary = null;
                                                pair.CustomAnimationFlag = 0;
                                                pair.IsCustomScenarioPlaying = false;
                                                pair.HasCustomScenarioStarted = false;
                                                pair.Character.Task.ClearAll();
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            Game.Player.Character.Task.ClearAll();
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.SyncEvent:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                        if (data != null)
                        {
                            var args = DecodeArgumentList(data.Arguments.ToArray()).ToList();
                            LogManager.DebugLog("RECEIVED SYNC EVENT " + ((SyncEventType)data.EventType) + ": " + args.Aggregate((f, s) => f.ToString() + ", " + s.ToString()));
                            switch ((SyncEventType)data.EventType)
                            {
                                case SyncEventType.LandingGearChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newState = (int)args[1];
                                        if (veh == null) return;
                                        Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, veh, newState);
                                    }
                                    break;
                                case SyncEventType.DoorStateChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var doorId = (int)args[1];
                                        var newFloat = (bool)args[2];
                                        if (veh == null) return;
                                        if (newFloat)
                                            new Vehicle(veh.Handle).OpenDoor((VehicleDoor)doorId, false, true);
                                        else
                                            new Vehicle(veh.Handle).CloseDoor((VehicleDoor)doorId, true);
                                    }
                                    break;
                                case SyncEventType.BooleanLights:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var lightId = (Lights)(int)args[1];
                                        var state = (bool)args[2];
                                        if (veh == null) return;
                                        if (lightId == Lights.NormalLights)
                                            new Vehicle(veh.Handle).LightsOn = state;
                                        else if (lightId == Lights.Highbeams)
                                            Function.Call(Hash.SET_VEHICLE_FULLBEAM, veh.Handle, state);
                                    }
                                    break;
                                case SyncEventType.TrailerDeTach:
                                    {
                                        var newState = (bool)args[0];
                                        if (!newState)
                                        {
                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            if (car != null)
                                            {
                                                if ((VehicleHash) car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash) car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    var trailer = Function.Call<Vehicle>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_TOW_TRUCK, trailer);
                                                }
                                                else if ((VehicleHash) car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash) car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash) car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash) car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    var trailer =
                                                        Function.Call<Vehicle>(Hash.GET_VEHICLE_ATTACHED_TO_CARGOBOB,
                                                            car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_CARGOBOB, trailer);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, car.Handle);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            var trailer = NetEntityHandler.NetToEntity((int)args[2]);
                                            if (car != null && trailer != null)
                                            {
                                                if ((VehicleHash)car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash)car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, car, trailer, true, 0, 0, 0);
                                                }
                                                else if ((VehicleHash)car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    new Vehicle(car.Handle).DropCargobobHook(CargobobHook.Hook);
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, car, 0, 0, 0, 0);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, car, trailer, 4f);
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case SyncEventType.TireBurst:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var tireId = (int)args[1];
                                        var isBursted = (bool)args[2];
                                        if (veh == null) return;
                                        if (isBursted)
                                            new Vehicle(veh.Handle).BurstTire(tireId);
                                        else
                                            new Vehicle(veh.Handle).FixTire(tireId);
                                    }
                                    break;
                                case SyncEventType.RadioChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newRadio = (int)args[1];
                                        if (veh != null)
                                        {
                                            var rad = (RadioStation)newRadio;
                                            string radioName = "OFF";
                                            if (rad != RadioStation.RadioOff)
                                            {
                                                radioName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME,
                                                    newRadio);
                                            }
                                            Function.Call(Hash.SET_VEH_RADIO_STATION, veh, radioName);
                                        }
                                    }
                                    break;
                                case SyncEventType.PickupPickedUp:
                                    {
                                        var pickupId = NetEntityHandler.NetToEntity((int)args[0]);
                                        if (pickupId != null && NetEntityHandler.IsPickup(pickupId.Handle))
                                        {
                                            Function.Call(Hash.REMOVE_PICKUP, pickupId.Handle);
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.PlayerDisconnect:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                        SyncPed target = null;
                        if (data != null && (target = NetEntityHandler.NetToStreamedItem(data.Id) as SyncPed) != null)
                        {
                            target.Clear();
                            NetEntityHandler.StreamOut(target);
                            lock (Npcs)
                            {
                                foreach (
                                    var pair in
                                        new Dictionary<string, SyncPed>(Npcs).Where(
                                            p => p.Value.Host == data.Id))
                                {
                                    pair.Value.Clear();
                                    Npcs.Remove(pair.Key);
                                }
                            }
                        }
                        NetEntityHandler.RemoveByNetHandle(data.Id);
                    }
                    break;
                case PacketType.ScriptEventTrigger:
                    {
                        var len = msg.ReadInt32();
                        var data =
                            DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                        if (data != null)
                        {
                            if (data.Arguments != null && data.Arguments.Count > 0)
                                JavascriptHook.InvokeServerEvent(data.EventName,
                                    DecodeArgumentListPure(data.Arguments?.ToArray()).ToArray());
                            else
                                JavascriptHook.InvokeServerEvent(data.EventName, new object[0]);
                        }
                    }
                    break;
                case PacketType.NativeCall:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                        if (data == null) return;
                        LogManager.DebugLog("RECEIVED NATIVE CALL " + data.Hash);
                        DecodeNativeCall(data);
                    }
                    break;
                case PacketType.NativeTick:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                        if (data == null) return;
                        lock (_tickNatives)
                        {
                            if (!_tickNatives.ContainsKey(data.Identifier))
                                _tickNatives.Add(data.Identifier, data.Native);

                            _tickNatives[data.Identifier] = data.Native;
                        }
                    }
                    break;
                case PacketType.NativeTickRecall:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                        if (data == null) return;
                        lock (_tickNatives)
                            if (_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Remove(data.Identifier);
                    }
                    break;
                case PacketType.NativeOnDisconnect:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                        if (data == null) return;
                        lock (_dcNatives)
                        {
                            if (!_dcNatives.ContainsKey(data.Id)) _dcNatives.Add(data.Id, data);
                            _dcNatives[data.Id] = data;
                        }
                    }
                    break;
                case PacketType.NativeOnDisconnectRecall:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                        if (data == null) return;
                        lock (_dcNatives) if (_dcNatives.ContainsKey(data.Id)) _dcNatives.Remove(data.Id);
                    }
                    break;
            }
            #endregion
        }

        public void ProcessMessages(NetIncomingMessage msg, bool safeThreaded)
        {
            PacketType type = PacketType.WorldSharingStop;
            LogManager.DebugLog("RECEIVED MESSAGE " + msg.MessageType);
            try
            {
                _messagesReceived++;
                _bytesReceived += msg.LengthBytes;
                if (msg.MessageType == NetIncomingMessageType.Data)
                {
                    type = (PacketType)msg.ReadInt32();
                    if (IsPacketTypeThreadsafe(type))
                    {
                        var pcmsgThread = new Thread((ThreadStart) delegate
                        {
                            ProcessDataMessage(msg, type);
                        });
                        pcmsgThread.IsBackground = true;
                        pcmsgThread.Start();
                    }
                    else
                    {
                        ProcessDataMessage(msg, type);
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.ConnectionLatencyUpdated)
                {
                    Latency = msg.ReadFloat();
                }
                else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    #region StatusChanged
                    var newStatus = (NetConnectionStatus) msg.ReadByte();
                    LogManager.DebugLog("NEW STATUS: " + newStatus);
                    switch (newStatus)
                    {
                        case NetConnectionStatus.InitiatedConnect:
                            Util.SafeNotify("Connecting...");
                            LocalTeam = -1;
                            LocalDimension = 0;
                            Game.Player.Character.Weapons.RemoveAll();
                            Game.Player.Character.Health = Game.Player.Character.MaxHealth;
                            Game.Player.Character.Armor = 0;
                            break;
                        case NetConnectionStatus.Connected:
                            Util.SafeNotify("Connection successful!");
                            var respLen = msg.SenderConnection.RemoteHailMessage.ReadInt32();
                            var respObj =
                                DeserializeBinary<ConnectionResponse>(
                                    msg.SenderConnection.RemoteHailMessage.ReadBytes(respLen)) as ConnectionResponse;
                            if (respObj == null)
                            {
                                Util.SafeNotify("ERROR WHILE READING REMOTE HAIL MESSAGE");
                                return;
                            }
                            NetEntityHandler.AddLocalCharacter(respObj.CharacterHandle);

                            var confirmObj = Client.CreateMessage();
                            confirmObj.Write((int) PacketType.ConnectionConfirmed);
                            confirmObj.Write(false);
                            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                            JustJoinedServer = true;
                            MainMenu.Tabs.RemoveAt(0);
                            MainMenu.Tabs.Insert(0, _serverItem);
                            MainMenu.Tabs.Insert(0, _mainMapItem);
                            MainMenu.RefreshIndex();
                            break;
                        case NetConnectionStatus.Disconnected:
                            var reason = msg.ReadString();
                            Util.SafeNotify("You have been disconnected" +
                                        (string.IsNullOrEmpty(reason) ? " from the server." : ": " + reason));
                            DEBUG_STEP = 40;
							// TODO: Dissect This
                            OnLocalDisconnect();
                            break;
                    }
                    #endregion
                }
                else if (msg.MessageType == NetIncomingMessageType.DiscoveryResponse)
                {
                    #region DiscoveryResponse
                    var discType = msg.ReadInt32();
                    var len = msg.ReadInt32();
                    var bin = msg.ReadBytes(len);
                    var data = DeserializeBinary<DiscoveryResponse>(bin) as DiscoveryResponse;
                    if (data == null) return;

                    var itemText = msg.SenderEndPoint.Address.ToString() + ":" + data.Port;

                    var matchedItems = new List<UIMenuItem>();

                    matchedItems.Add(_serverBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems.Add(_recentBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems.Add(_favBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems.Add(_lanBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems = matchedItems.Distinct().ToList();

                    _currentOnlinePlayers += data.PlayerCount;

                    MainMenu.Money = "Servers Online: " + ++_currentOnlineServers + " | Players Online: " + _currentOnlinePlayers;
                        
                    if (data.LAN) //  && matchedItems.Count == 0
                    {
                        var item = new UIMenuItem(data.ServerName);
                        var gamemode = data.Gamemode == null ? "Unknown" : data.Gamemode;

                        item.Text = data.ServerName;
                        item.Description = itemText;
                        item.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);

                        if (data.PasswordProtected)
                            item.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                        int lastIndx = 0;
                        if (_serverBrowser.Items.Count > 0)
                            lastIndx = _serverBrowser.Index;

                        var gMsg = msg;
                        item.Activated += (sender, selectedItem) =>
                        {
                            if (IsOnServer())
                            {
                                Client.Disconnect("Switching servers.");
                                NetEntityHandler.ClearAll();

                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }

                                while (IsOnServer()) Script.Yield();
                            }

                            if (data.PasswordProtected)
                            {
                                _password = Game.GetUserInput(256);
                            }

                            _connectTab.RefreshIndex();
                            ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port);
                            MainMenu.TemporarilyHidden = true;
                            AddServerToRecent(item);
                        };

                        _lanBrowser.Items.Add(item);
                    }
                        

                    foreach (var ourItem in matchedItems.Where(k => k != null))
                    {
                        var gamemode = data.Gamemode == null ? "Unknown" : data.Gamemode;

                        ourItem.Text = data.ServerName;
                        ourItem.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);
                        if (PlayerSettings.FavoriteServers.Contains(ourItem.Description))
                            ourItem.SetRightBadge(UIMenuItem.BadgeStyle.Star);

                        if (data.PasswordProtected)
                            ourItem.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                        int lastIndx = 0;
                        if (_serverBrowser.Items.Count > 0)
                            lastIndx = _serverBrowser.Index;

                        var gMsg = msg;
                        var data1 = data;
                        ourItem.Activated += (sender, selectedItem) =>
                        {
                            if (IsOnServer())
                            {
                                Client.Disconnect("Switching servers.");


                                NetEntityHandler.ClearAll();

                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }

                                while (IsOnServer()) Script.Yield();
                            }

                            if (data1.PasswordProtected)
                            {
                                _password = Game.GetUserInput(256);
                            }


                            ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data1.Port);
                            MainMenu.TemporarilyHidden = true;
                            _connectTab.RefreshIndex();
                            AddServerToRecent(ourItem);
                        };


                        if (_serverBrowser.Items.Contains(ourItem))
                        {
                            _serverBrowser.Items.Remove(ourItem);
                            _serverBrowser.Items.Insert(0, ourItem);
                            if (_serverBrowser.Focused)
                                _serverBrowser.MoveDown();
                            else
                                _serverBrowser.RefreshIndex();
                        }
                        else if (_lanBrowser.Items.Contains(ourItem))
                        {
                            _lanBrowser.Items.Remove(ourItem);
                            _lanBrowser.Items.Insert(0, ourItem);
                            if (_lanBrowser.Focused)
                                _lanBrowser.MoveDown();
                            else
                                _lanBrowser.RefreshIndex();
                        }
                        else if (_favBrowser.Items.Contains(ourItem))
                        {
                            _favBrowser.Items.Remove(ourItem);
                            _favBrowser.Items.Insert(0, ourItem);
                            if (_favBrowser.Focused)
                                _favBrowser.MoveDown();
                            else
                                _favBrowser.RefreshIndex();
                        }
                        else if (_recentBrowser.Items.Contains(ourItem))
                        {
                            _recentBrowser.Items.Remove(ourItem);
                            _recentBrowser.Items.Insert(0, ourItem);
                            if (_recentBrowser.Focused)
                                _recentBrowser.MoveDown();
                            else
                                _recentBrowser.RefreshIndex();
                        }
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                if (safeThreaded)
                {
                    Util.SafeNotify("Unhandled Exception ocurred in Process Messages");
                    Util.SafeNotify("Message Type: " + msg.MessageType);
                    Util.SafeNotify("Data Type: " + type);
                    Util.SafeNotify(e.Message);
                }
                LogManager.LogException(e, "PROCESS MESSAGES (TYPE: " + msg.MessageType + " DATATYPE: " + type + ")");
            }

            //Client.Recycle(msg);
        }

        private void HandleBasicPacket(int nethandle, Vector3 position)
        {
            var syncPed = NetEntityHandler.GetPlayer(nethandle);

            syncPed.Position = position;
            syncPed.VehiclePosition = position;
        }

        private void HandleVehiclePacket(VehicleData fullData)
        {
            var syncPed = NetEntityHandler.GetPlayer(fullData.NetHandle.Value);

            syncPed.LastUpdateReceived = Environment.TickCount;
            syncPed.IsInVehicle = true;

            if (fullData.VehicleHandle != null) LogManager.DebugLog("=====RECEIVED LIGHT VEHICLE PACKET " + fullData.VehicleHandle);

            if (fullData.Position != null) syncPed.VehiclePosition = fullData.Position.ToVector();
            if (fullData.VehicleHandle != null) syncPed.VehicleNetHandle = fullData.VehicleHandle.Value;
            if (fullData.Velocity != null) syncPed.VehicleVelocity = fullData.Velocity.ToVector();
            if (fullData.PedModelHash != null) syncPed.ModelHash = fullData.PedModelHash.Value;
            if (fullData.PedArmor != null) syncPed.PedArmor = fullData.PedArmor.Value;
            if (fullData.RPM != null) syncPed.VehicleRPM = fullData.RPM.Value;
            if (fullData.Quaternion != null) syncPed.VehicleRotation = fullData.Quaternion.ToVector();
            if (fullData.PlayerHealth != null) syncPed.PedHealth = fullData.PlayerHealth.Value;
            if (fullData.VehicleHealth != null) syncPed.VehicleHealth = fullData.VehicleHealth.Value;
            if (fullData.VehicleSeat != null) syncPed.VehicleSeat = fullData.VehicleSeat.Value;
            if (fullData.Latency != null) syncPed.Latency = fullData.Latency.Value;
            if (fullData.Steering != null) syncPed.SteeringScale = fullData.Steering.Value;
            if (fullData.Velocity != null) syncPed.Speed = fullData.Velocity.ToVector().Length();

            if (fullData.Flag != null)
            {
                syncPed.IsVehDead = (fullData.Flag.Value & (short)VehicleDataFlags.VehicleDead) > 0;
                syncPed.IsHornPressed = (fullData.Flag.Value & (short)VehicleDataFlags.PressingHorn) > 0;
                syncPed.Siren = (fullData.Flag.Value & (short)VehicleDataFlags.SirenActive) > 0;
                syncPed.IsShooting = (fullData.Flag.Value & (short)VehicleDataFlags.Shooting) > 0;
                syncPed.IsAiming = (fullData.Flag.Value & (short)VehicleDataFlags.Aiming) > 0;
            }

            if (fullData.WeaponHash != null)
            {
                syncPed.CurrentWeapon = fullData.WeaponHash.Value;
            }

            if (fullData.AimCoords != null) syncPed.AimCoords = fullData.AimCoords.ToVector();

            if (syncPed.VehicleNetHandle != 0 && fullData.Position != null)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null) car.Position = fullData.Position;
            }
            else if (syncPed.VehicleNetHandle != 00 && fullData.Position == null)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    syncPed.Position = car.Position.ToVector();
                    syncPed.VehiclePosition = car.Position.ToVector();
                }
            }
        }

        private void HandlePedPacket(PedData fullPacket)
        {
            var syncPed = NetEntityHandler.GetPlayer(fullPacket.NetHandle.Value);

            syncPed.LastUpdateReceived = Environment.TickCount;
            syncPed.IsInVehicle = false;
            syncPed.VehicleNetHandle = 0;

            if (fullPacket.Position != null) syncPed.Position = fullPacket.Position.ToVector();
            if (fullPacket.Speed != null) syncPed.OnFootSpeed = fullPacket.Speed.Value;
            if (fullPacket.PedArmor != null) syncPed.PedArmor = fullPacket.PedArmor.Value;
            if (fullPacket.PedModelHash != null) syncPed.ModelHash = fullPacket.PedModelHash.Value;
            if (fullPacket.Quaternion != null) syncPed.Rotation = fullPacket.Quaternion.ToVector();
            if (fullPacket.PlayerHealth != null) syncPed.PedHealth = fullPacket.PlayerHealth.Value;
            if (fullPacket.AimCoords != null) syncPed.AimCoords = fullPacket.AimCoords.ToVector();
            if (fullPacket.WeaponHash != null) syncPed.CurrentWeapon = fullPacket.WeaponHash.Value;
            if (fullPacket.Latency != null) syncPed.Latency = fullPacket.Latency.Value;
            if (fullPacket.Velocity != null) syncPed.PedVelocity = fullPacket.Velocity.ToVector();

            if (fullPacket.Flag != null)
            {
                syncPed.IsFreefallingWithParachute = (fullPacket.Flag.Value & (int)PedDataFlags.InFreefall) >
                                                     0;
                syncPed.IsInMeleeCombat = (fullPacket.Flag.Value & (int)PedDataFlags.InMeleeCombat) > 0;
                syncPed.IsRagdoll = (fullPacket.Flag.Value & (int)PedDataFlags.Ragdoll) > 0;
                syncPed.IsAiming = (fullPacket.Flag.Value & (int)PedDataFlags.Aiming) > 0;
                syncPed.IsJumping = (fullPacket.Flag.Value & (int)PedDataFlags.Jumping) > 0;
                syncPed.IsShooting = (fullPacket.Flag.Value & (int)PedDataFlags.Shooting) > 0;
                syncPed.IsParachuteOpen = (fullPacket.Flag.Value & (int)PedDataFlags.ParachuteOpen) > 0;
                syncPed.IsInCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCover) > 0;
                syncPed.IsInLowCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInLowerCover) > 0;
                syncPed.IsCoveringToLeft = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCoverFacingLeft) >
                                           0;
                syncPed.IsReloading = (fullPacket.Flag.Value & (int)PedDataFlags.IsReloading) > 0;
            }
        }

	    private void ClearLocalEntities()
	    {
			lock (EntityCleanup)
			{
				EntityCleanup.ForEach(ent =>
				{
				    var prop = new Prop(ent);
                    if (prop.Exists()) prop.Delete();
				});
				EntityCleanup.Clear();
			}
		}

	    private void ClearLocalBlips()
	    {
			lock (BlipCleanup)
			{
				BlipCleanup.ForEach(blip =>
				{
				    var b = new Blip(blip);
                    if (b.Exists()) b.Remove();
				});
				BlipCleanup.Clear();
			}
		}

	    private void RestoreMainMenu()
	    {
			MainMenu.TemporarilyHidden = false;
			JustJoinedServer = false;

			DEBUG_STEP = 53;

			MainMenu.Tabs.Remove(_serverItem);
			MainMenu.Tabs.Remove(_mainMapItem);
			DEBUG_STEP = 54;
			if (!MainMenu.Tabs.Contains(_welcomePage))
				MainMenu.Tabs.Insert(0, _welcomePage);
			DEBUG_STEP = 55;
			MainMenu.RefreshIndex();
			_localMarkers.Clear();

		}

	    private void ResetWorld()
	    {

			World.RenderingCamera = MainMenuCamera;
			MainMenu.Visible = true;
			MainMenu.TemporarilyHidden = false;
			IsSpectating = false;
			Weather = null;
			Time = null;
			LocalTeam = -1;
	        LocalDimension = 0;
			DEBUG_STEP = 57;

			Game.Player.Character.Position = _vinewoodSign;
	        Game.Player.Character.FreezePosition = false;

			Util.SetPlayerSkin(PedHash.Clown01SMY);

	        Game.Player.Character.MaxHealth = 200;
	        Game.Player.Character.Health = 200;

            Game.Player.Character.FreezePosition = false;
            Game.Player.IsInvincible = false;
            Game.Player.Character.HasCollision = true;

            //Script.Wait(500);
            //Game.Player.Character.SetDefaultClothes();
        }

		private void OnLocalDisconnect()
	    {
			DEBUG_STEP = 43;
			
			ClearLocalEntities();

			DEBUG_STEP = 47;

			ClearLocalBlips();

			DEBUG_STEP = 48;

			Chat.Clear();
			DEBUG_STEP = 49;

			NetEntityHandler.ClearAll();
			DEBUG_STEP = 50;
			JavascriptHook.StopAllScripts();
            JavascriptHook.TextElements.Clear();
			DEBUG_STEP = 51;
			DownloadManager.Cancel();
		    HasFinishedDownloading = false;
			DEBUG_STEP = 52;

            ClearStats();

			RestoreMainMenu();

			DEBUG_STEP = 56;

			ResetWorld();

            if (_serverProcess != null)
            {
                UI.Notify("~b~~h~GTA Network~h~~w~~n~Shutting down server...");
		        _serverProcess.Kill();
                _serverProcess.Dispose();
		        _serverProcess = null;
		    }
		}

        void ClearStats()
        {
            _bytesReceived = 0;
            _bytesSent = 0;
            _messagesReceived = 0;
            _messagesSent = 0;
        }

        #region debug stuff

        private DateTime _artificialLagCounter = DateTime.MinValue;
        private bool _debugStarted;
        private SyncPed _debugSyncPed;
        private int _debugPing = 150;
        private DateTime _lastPingTime;
        private int _debugInterval = 30;
        private int _debugFluctuation = 0;
        private Camera _debugCamera;
        private Random _r = new Random();
        private List<Tuple<int, object>> _lastData = new List<Tuple<int, object>>();
        private void Debug()
        {
            var player = Game.Player.Character;
            
            if (_debugSyncPed == null)
            {
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Rotation, false);
                _debugSyncPed.Debug = true;
                _debugSyncPed.StreamedIn = true;
            }

            if (Game.IsKeyPressed(Keys.NumPad1) && _debugInterval > 0)
            {
                _debugInterval--;
                UI.ShowSubtitle("SIMULATED PING: " + _debugInterval, 5000);
            }
            else if (Game.IsKeyPressed(Keys.NumPad2))
            {
                _debugInterval++;
                UI.ShowSubtitle("SIMULATED PING: " + _debugInterval, 5000);
            }

            _lastData.Add(new Tuple<int, object>(Environment.TickCount,
                player.IsInVehicle() ? (object) PackageVehicleData() : (object) PackagePedData()));

            if (Environment.TickCount - _lastData[0].Item1 >= (_debugInterval))
            {
                //_artificialLagCounter = DateTime.Now;
                //_debugFluctuation = _r.Next(10) - 5;

                var ourData = _lastData[0].Item2;
                _lastData.RemoveAt(0);
                
                _debugSyncPed.Snapshot = ourData;

                if (ourData is VehicleData)
                {
                    if (player.IsInVehicle())
                        player.CurrentVehicle.Alpha = 50;

                    var data = (VehicleData) ourData;

                    _debugSyncPed.VehicleNetHandle = data.VehicleHandle.Value;
                    _debugSyncPed.LastUpdateReceived = Environment.TickCount;
                    _debugSyncPed.VehiclePosition = data.Position.ToVector();
                    _debugSyncPed.VehicleVelocity = data.Velocity.ToVector();
                    _debugSyncPed.ModelHash = data.PedModelHash.Value;
                    //_debugSyncPed.VehicleHash = data.VehicleModelHash.Value;
                    _debugSyncPed.PedArmor = data.PedArmor.Value;
                    _debugSyncPed.VehicleRPM = data.RPM.Value;
                    _debugSyncPed.VehicleRotation =
                        data.Quaternion.ToVector();
                    _debugSyncPed.PedHealth = data.PlayerHealth.Value;
                    _debugSyncPed.VehicleHealth = data.VehicleHealth.Value;
                    _debugSyncPed.VehicleSeat = data.VehicleSeat.Value;
                    _debugSyncPed.IsInVehicle = true;
                    _debugSyncPed.Latency = data.Latency.Value;
                    _debugSyncPed.SteeringScale = data.Steering.Value;
                    _debugSyncPed.IsVehDead = (data.Flag & (short)VehicleDataFlags.VehicleDead) > 0;
                    _debugSyncPed.IsHornPressed = (data.Flag & (short)VehicleDataFlags.PressingHorn) > 0;
                    _debugSyncPed.Speed = data.Velocity.ToVector().Length();
                    _debugSyncPed.Siren = (data.Flag & (short)VehicleDataFlags.SirenActive) > 0;
                    _debugSyncPed.IsShooting = (data.Flag & (short)VehicleDataFlags.Shooting) > 0;
                    _debugSyncPed.IsAiming = (data.Flag & (short)VehicleDataFlags.Aiming) > 0;
                    _debugSyncPed.CurrentWeapon = data.WeaponHash.Value;
                    if (data.AimCoords != null)
                        _debugSyncPed.AimCoords = data.AimCoords.ToVector();

                    //if (_debugCamera == null)
                    //_debugCamera = World.CreateCamera(player.Position + new Vector3(0, 0, 10f), new Vector3(), 60f);
                    //_debugCamera.PointAt(player);
                    //_debugCamera.Position = player.GetOffsetInWorldCoords(new Vector3(0, -10f, 20f));
                    //World.RenderingCamera = _debugCamera;
                }
                else
                {
                    var data = (PedData) ourData;

                    _debugSyncPed.IsRagdoll = player.IsRagdoll;
                    _debugSyncPed.OnFootSpeed = data.Speed.Value;
                    _debugSyncPed.PedArmor = data.PedArmor.Value;
                    _debugSyncPed.LastUpdateReceived = Environment.TickCount;
                    _debugSyncPed.Position = data.Position.ToVector();
                    _debugSyncPed.ModelHash = data.PedModelHash.Value;
                    _debugSyncPed.Rotation = data.Quaternion.ToVector();
                    _debugSyncPed.PedHealth = data.PlayerHealth.Value;
                    _debugSyncPed.IsInVehicle = false;
                    _debugSyncPed.AimCoords = data.AimCoords.ToVector();
                    _debugSyncPed.CurrentWeapon = data.WeaponHash.Value;
                    _debugSyncPed.Latency = data.Latency.Value;
                    _debugSyncPed.PedVelocity = data.Velocity.ToVector();
                    _debugSyncPed.IsFreefallingWithParachute = (data.Flag & (int)PedDataFlags.InFreefall) > 0;
                    _debugSyncPed.IsInMeleeCombat = (data.Flag & (int)PedDataFlags.InMeleeCombat) > 0;
                    _debugSyncPed.IsRagdoll = (data.Flag & (int)PedDataFlags.Ragdoll) > 0;
                    _debugSyncPed.IsAiming = (data.Flag & (int)PedDataFlags.Aiming) > 0;
                    _debugSyncPed.IsJumping = (data.Flag & (int)PedDataFlags.Jumping) > 0;
                    _debugSyncPed.IsShooting = (data.Flag & (int)PedDataFlags.Shooting) > 0;
                    _debugSyncPed.IsParachuteOpen = (data.Flag & (int)PedDataFlags.ParachuteOpen) > 0;
                    _debugSyncPed.IsInCover = (data.Flag & (int)PedDataFlags.IsInCover) > 0;
                    _debugSyncPed.IsInLowCover = (data.Flag & (int)PedDataFlags.IsInLowerCover) > 0;
                    _debugSyncPed.IsCoveringToLeft = (data.Flag & (int)PedDataFlags.IsInCoverFacingLeft) > 0;
                    _debugSyncPed.IsReloading = (data.Flag & (int)PedDataFlags.IsReloading) > 0;
                }
            }

            _debugSyncPed.DisplayLocally();

            if (_debugSyncPed.Character != null)
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.Character.Handle, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, _debugSyncPed.Character.Handle, false);
            }


            if (_debugSyncPed.MainVehicle != null && player.IsInVehicle())
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.MainVehicle.Handle, player.CurrentVehicle.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle, _debugSyncPed.MainVehicle.Handle, false);
            }

        }
        
        #endregion

        public IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
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

        public IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
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
                    list.Add(new LocalHandle(NetEntityHandler.NetToEntity(((EntityArgument)arg).NetHandle)?.Handle ?? 0));
                }
                else if (arg is EntityPointerArgument)
                {
                    list.Add(new OutputArgument(NetEntityHandler.NetToEntity(((EntityPointerArgument)arg).NetHandle)));
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static void SendToServer(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Client.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Client.SendMessage(msg, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int)channel);
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
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static void TriggerServerEvent(string eventName, params object[] args)
        {
            if (!IsOnServer()) return;
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            packet.Arguments = ParseNativeArguments(args);
            var bin = SerializeBinary(packet);

            var msg = Client.CreateMessage();
            msg.Write((int)PacketType.ScriptEventTrigger);
            msg.Write(bin.Length);
            msg.Write(bin);

            Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void DecodeNativeCall(NativeData obj)
        {
            var list = new List<InputArgument>();

            var objectList = DecodeArgumentList(obj.Arguments.ToArray());

            list.AddRange(objectList.Select(ob => ob is OutputArgument ? (OutputArgument)ob : new InputArgument(ob)));

            LogManager.DebugLog("NATIVE CALL ARGUMENTS: " + objectList.Aggregate((f, s) => f + ", " + s));
            LogManager.DebugLog("RETURN TYPE: " + obj.ReturnType);
            var nativeType = CheckNativeHash(obj.Hash);
            LogManager.DebugLog("NATIVE TYPE IS " + nativeType);
            Model model = null;
            if (((int)nativeType & (int)NativeType.NeedsModel) > 0)
            {
                LogManager.DebugLog("REQUIRES MODEL");
                int position = 0;
                if (((int)nativeType & (int)NativeType.NeedsModel1) > 0)
                    position = 0;
                if (((int)nativeType & (int)NativeType.NeedsModel2) > 0)
                    position = 1;
                if (((int)nativeType & (int)NativeType.NeedsModel3) > 0)
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

            if (((int)nativeType & (int)NativeType.ReturnsEntity) > 0)
            {
                var entId = Function.Call<int>((Hash) obj.Hash, list.ToArray());
                lock(EntityCleanup) EntityCleanup.Add(entId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, entId);
                }

                if (model != null)
                    model.MarkAsNoLongerNeeded();
                return;
            }

            if (nativeType == NativeType.ReturnsBlip)
            {
                var blipId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (BlipCleanup) BlipCleanup.Add(blipId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, blipId);
                }
                return;
            }

            if (((int) nativeType & (int) NativeType.TimeSet) > 0)
            {
                var newHours = ((IntArgument) obj.Arguments[0]).Data;
                var newMinutes = ((IntArgument)obj.Arguments[1]).Data;
                Time = new TimeSpan(newHours, newMinutes, 0);
            }

            if (((int)nativeType & (int)NativeType.WeatherSet) > 0)
            {
                var newWeather = ((StringArgument)obj.Arguments[0]).Data;
                Weather = newWeather;
            }

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
        }

        public void SendNativeCallResponse(string id, object response)
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
            msg.Write((int)PacketType.NativeResponse);
            msg.Write(bin.Length);
            msg.Write(bin);
            Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }
        
        private enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1 << 1,
            ReturnsEntity = 1 << 2,
            NeedsModel = 1 << 3,
            NeedsModel1 = 1 << 4,
            NeedsModel2 = 1 << 5,
            NeedsModel3 = 1 << 6,
            TimeSet = 1 << 7,
            WeatherSet = 1 << 8,
        }

        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                case 0x00A1CADD00108836:
                    return NativeType.NeedsModel2 | NativeType.Unknown | NativeType.NeedsModel;
                case 0xD49F9B0955C367DE:
                    return NativeType.NeedsModel2 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0x7DD959874C1FD534:
                    return NativeType.NeedsModel3 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return NativeType.NeedsModel1 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return NativeType.ReturnsEntity;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
                case 0x47C3B5848C3E45D8:
                    return NativeType.TimeSet;
                case 0xED712CA327900C8A:
                    return NativeType.WeatherSet;
            }
        }

        public static int GetPedSpeed(Vector3 firstVector, Vector3 secondVector)
        {
            float speed = (firstVector - secondVector).Length();
            if (speed < 0.02f)
            {
                return 0;
            }
            else if (speed >= 0.02f && speed < 0.05f)
            {
                return 1;
            }
            else if (speed >= 0.05f && speed < 0.12f)
            {
                return 2;
            }
            else if (speed >= 0.12f)
                return 3;
            return 0;
        }

        public static bool WorldToScreenRel(Vector3 worldCoords, out Vector2 screenCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                screenCoords = new Vector2();
                return false;
            }
            screenCoords = new Vector2((num1.GetResult<float>() - 0.5f) * 2, (num2.GetResult<float>() - 0.5f) * 2);
            return true;
        }

        public static Vector3 ScreenRelToWorld(Vector3 camPos, Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new Vector3(10, 0, 0);
            var rotDown = camRot + new Vector3(-10, 0, 0);
            var rotLeft = camRot + new Vector3(0, 0, -10);
            var rotRight = camRot + new Vector3(0, 0, 10);

            var camRight = RotationToDirection(rotRight) - RotationToDirection(rotLeft);
            var camUp = RotationToDirection(rotUp) - RotationToDirection(rotDown);

            var rollRad = -DegToRad(camRot.Y);

            var camRightRoll = camRight * (float)Math.Cos(rollRad) - camUp * (float)Math.Sin(rollRad);
            var camUpRoll = camRight * (float)Math.Sin(rollRad) + camUp * (float)Math.Cos(rollRad);

            var point3D = camPos + camForward * 10.0f + camRightRoll + camUpRoll;
            Vector2 point2D;
            if (!WorldToScreenRel(point3D, out point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            Vector2 point2DZero;
            if (!WorldToScreenRel(point3DZero, out point2DZero)) return camPos + camForward * 10.0f;

            const double eps = 0.001;
            if (Math.Abs(point2D.X - point2DZero.X) < eps || Math.Abs(point2D.Y - point2DZero.Y) < eps) return camPos + camForward * 10.0f;
            var scaleX = (coord.X - point2DZero.X) / (point2D.X - point2DZero.X);
            var scaleY = (coord.Y - point2DZero.Y) / (point2D.Y - point2DZero.Y);
            var point3Dret = camPos + camForward * 10.0f + camRightRoll * scaleX + camUpRoll * scaleY;
            return point3Dret;
        }

        public static Vector3 RotationToDirection(Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static Vector3 DirectionToRotation(Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double DegToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public static double RadToDeg(double deg)
        {
            return deg * 180.0 / Math.PI;
        }

        public static double BoundRotationDeg(double angleDeg)
        {
            var twoPi = (int)(angleDeg / 360);
            var res = angleDeg - twoPi * 360;
            if (res < 0) res += 360;
            return res;
        }

        public static Vector3 RaycastEverything(Vector2 screenCoord)
        {
            var camPos = GameplayCamera.Position;
            var camRot = GameplayCamera.Rotation;
            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Entity ignoreEntity = Game.Player.Character;
            if (Game.Player.Character.IsInVehicle())
            {
                ignoreEntity = Game.Player.Character.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHitAnything)
            {
                return raycastResults.HitCoords;
            }

            return camPos + dir * raycastToDist;
        }

        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException)
                {
                    return null;
                }
            }
            return output;
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public int GetOpenUdpPort()
        {
            var startingAtPort = 6000;
            var maxNumberOfPortsToCheck = 500;
            var range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            var portsInUse =
                from p in range
                join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            on p equals used.Port
                select p;

            return range.Except(portsInUse).FirstOrDefault();
        }

        public void TerminateGameScripts()
        {
            if (PlayerSettings.DisableRockstarEditor)
            {
                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "replay_controller");
                Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "selector");
            }

            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "abigail1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "abigail2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "achievement_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "act_cinema");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "af_intro_t_sandy");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_heist1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_heist2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_heist3a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_heist3b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_prep1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "agency_prep2amb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "aicover_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ainewengland_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "altruist_cult");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambientblimp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_diving");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_mrsphilips");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_solomon");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_sonar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_tonya");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_tonyacall");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_tonyacall2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_tonyacall5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ambient_ufos");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_airstrike");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_ammo_drop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_armwrestling");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_armybase");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_backup_heli");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_boat_taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_bru_box");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_car_mod_tut");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_challenges");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_contact_requests");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_cp_collection");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_crate_drop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_criminal_damage");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_cr_securityvan");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_darts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_dead_drop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_destroy_veh");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_distract_cops");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_doors");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_ferriswheel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_gang_call");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_ga_pickups");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_heist_int");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_heli_taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_hold_up");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_hot_property");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_hot_target");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_hunt_the_beast");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_imp_exp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_joyrider");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_kill_list");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_king_of_the_castle");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_launcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_lester_cut");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_lowrider_int");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mission_launch");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mp_carwash_launch");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mp_garage_control");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mp_property_ext");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mp_property_int");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_mp_yacht");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_npc_invites");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_pass_the_parcel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_penned_in");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_pi_menu");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_plane_takedown");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_prison");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_prostitute");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_rollercoaster");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_rontrevor_cut");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "am_vehicle_spawn");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "animal_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appbroadcast");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appcamera");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appchecklist");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appcontacts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appemail");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appextraction");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "apphs_sleep");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appinternet");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appjipmp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appmedia");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appmpbossagency");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appmpemail");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appmpjoblistnew");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "apporganiser");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "apprepeatplay");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appsettings");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appsidetask");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "apptextmessage");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "apptrackify");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appvlsi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "appzit");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "armenian1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "armenian2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "armenian3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_bus");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_construction");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_hooker");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_multi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_rankup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "assassin_valet");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "atm_trigger");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "audiotest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "autosave_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bailbond1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bailbond2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bailbond3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bailbond4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bailbond_launcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry3a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry3c");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "barry4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "benchmark");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bigwheel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bj");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "blimptest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "blip_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bootycallhandler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bootycall_debug_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "buddydeathresponse");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "bugstar_mission_export");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "buildingsiteambience");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "building_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cablecar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "camera_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cam_coord_sender");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "candidate_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carmod_shop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carsteal1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carsteal2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carsteal3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carsteal4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carwash1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "carwash2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "car_roof_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "celebrations");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "celebration_editor");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cellphone_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cellphone_flashhand");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "charactergoals");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "charanimtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cheat_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "chinese1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "chinese2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "chop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "clothes_shop_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "clothes_shop_sp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "code_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "combat_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "comms_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "completionpercentage_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "component_checker");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "context_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "controller_ambientarea");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "controller_races");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "controller_taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "controller_towing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "controller_trafficking");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "coordinate_recorder");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "country_race");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "country_race_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "creation_startup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "custom_config");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cutscenemetrics");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cutscenesamples");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cutscene_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "darts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "debug");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "debug_app_select_screen");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "debug_launcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "density_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "dialogue_handler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "director_mode");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks2asubhandler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks_heista");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks_heistb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks_prep1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks_prep2b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "docks_setup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "dreyfuss1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drf1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drf2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drf3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drf4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drf5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drunk");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "drunk_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "dynamixtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "email_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "emergencycall");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "emergencycalllauncher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epscars");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsdesert");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon6");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon7");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilon8");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsilontract");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "epsrobes");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "event_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "exile1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "exile2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "exile3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "exile_city_denial");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "extreme1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "extreme2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "extreme3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "extreme4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fairgroundhub");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fake_interiors");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fameorshame_eps");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fameorshame_eps_1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fame_or_shame_set");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family1taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family6");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family_scene_f0");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family_scene_f1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family_scene_m");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family_scene_t0");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "family_scene_t1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fanatic1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fanatic2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fanatic3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_intro");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep3amb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi4_prep5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fbi5a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "filenames.txt");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finalea");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finaleb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finalec1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finalec2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_choice");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_credits");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_endgame");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist2a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist2b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist2_intro");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist_prepa");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist_prepb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist_prepc");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist_prepd");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_heist_prepeamb");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "finale_intro");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "floating_help_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flowintrotitle");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flowstartaccept");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flow_autoplay");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flow_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flow_help");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "flyunderbridges");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fmmc_launcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fmmc_playlist_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_bj_race_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_capture_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_deathmatch_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_deathmatch_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_hideout_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_hold_up_tut");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_horde_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_impromptu_dm_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_intro");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_intro_cut_dev");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_lts_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_maintain_cloud_header_data");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_maintain_transition_players");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_main_menu");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_mission_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_mission_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_race_controler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fm_race_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "forsalesigns");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fps_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fps_test_mag");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "franklin0");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "franklin1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "franklin2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "freemode");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "freemode_init");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "friendactivity");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "friends_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "friends_debug_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fullmap_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "fullmap_test_flow");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "game_server_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_assault");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_bellybeast");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_carjacking");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_collect_money");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_deathmatch");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_finderskeepers");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_fivestar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_hunt_the_boss");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_point_to_point");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_rob_shop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_sightseer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_terminate");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gb_yacht_rob");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "general_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "golf");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "golf_ai_foursome");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "golf_ai_foursome_putting");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "golf_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_andymoon");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_baygor");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_billbinder");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_clinton");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_griff");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_jane");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_jerome");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_jesse");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_mani");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_mime");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_pameladrake");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_superhero");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_tonya");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gpb_zombie");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_airplane");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_avoidance");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_boat");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_divingfromcar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_divingfromcarwhilefleeing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_helicopter");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gtest_nearlymissedbycar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gunclub_shop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "gunfighttest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hairdo_shop_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hairdo_shop_sp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hao1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "headertest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heatmap_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heatmap_test_flow");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heist_ctrl_agency");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heist_ctrl_docks");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heist_ctrl_finale");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heist_ctrl_jewel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heist_ctrl_rural");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heli_gun");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "heli_streaming");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hud_creator");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hunting1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hunting2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "hunting_ambient");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "idlewarper");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ingamehud");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "initial");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "jewelry_heist");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "jewelry_prep1a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "jewelry_prep1b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "jewelry_prep2a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "jewelry_setup1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "josh1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "josh2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "josh3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "josh4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "lamar1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "laptop_trigger");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_abigail");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_barry");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_basejumpheli");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_basejumppack");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_carwash");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_darts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_dreyfuss");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_epsilon");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_extreme");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_fanatic");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_golf");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_hao");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_hunting");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_hunting_ambient");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_josh");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_maude");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_minute");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_mrsphilips");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_nigel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_offroadracing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_omega");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_paparazzo");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_pilotschool");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_racing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_rampage");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_range");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_stunts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_tennis");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_thelastone");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_tonya");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_triathlon");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "launcher_yoga");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "lester1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "lesterhandler");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "letterscraps");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "line_activation_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "liverecorder");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "locates_tester");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "luxe_veh_activity");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "magdemo");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "magdemo2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "main");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "maintransition");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "main_install");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "main_persistent");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "martin1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "maude1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "maude_postbailbond");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "me_amanda1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "me_jimmy1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "me_tracey1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mg_race_to_point");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "michael1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "michael2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "michael3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "michael4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "michael4leadout");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "minigame_ending_stinger");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "minigame_stats_tracker");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "minute1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "minute2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "minute3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_race");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_repeat_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_stat_alerter");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_stat_watcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_triggerer_a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_triggerer_b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_triggerer_c");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mission_triggerer_d");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mpstatsinit");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mptestbed");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_awards");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_fm_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_menuped");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_prop_global_block");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_prop_special_global_block");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_save_game_global_block");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_unlocks");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mp_weapons");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mrsphilips1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "mrsphilips2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "murdermystery");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "navmeshtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_bot_brain");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_bot_simplebrain");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_cloud_mission_loader");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_combat_soaktest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_jacking_soaktest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_rank_tunable_loader");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_session_soaktest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "net_tunable_check");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel1a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel1b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel1c");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel1d");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nigel3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "nodeviewer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_abatdoor");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_abattoircut");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_airdancer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_bong");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_cashregister");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_drinking_shots");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_foundry_cauldron");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_franklin_beer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_franklin_tv");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_franklin_wine");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_huffing_gas");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mp_bed_high");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mp_bed_low");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mp_bed_med");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mp_shower_med");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mp_stripper");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_mr_raspberry_jam");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_poledancer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_sofa_franklin");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_sofa_michael");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_telescope");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_tv");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_vend1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_vend2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ob_wheatgrass");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "offroad_races");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "omega1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "omega2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo3a");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo3b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paparazzo4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paradise");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "paradise2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pausemenu");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pausemenu_example");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pausemenu_map");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pausemenu_multiplayer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pausemenu_sp_repeat");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pb_busker");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pb_homeless");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pb_preacher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pb_prostitute");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "photographymonkey");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "photographywildlife");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "physics_perf_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "physics_perf_test_launcher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pickuptest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pickupvehicles");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pickup_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pilot_school");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pilot_school_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pi_menu");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "placeholdermission");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "placementtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "planewarptest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_controller_b");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_ft_franklin1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_f_lamgraff");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_f_lamtaunt");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_f_taxi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_mf_traffic");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_m_cinema");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_m_fbi2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_m_kids");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_m_shopping");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_t_bbfight");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_t_chasecar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_t_insult");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_t_park");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_scene_t_tie");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "player_timetable_scene");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "playthrough_builder");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pm_defend");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pm_delivery");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pm_gang_attack");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pm_plane_promotion");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "pm_recover_stolen");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "postkilled_bailbond2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "postrc_barry1and2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "postrc_barry4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "postrc_epsilon4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "postrc_nigel3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "profiler_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "prologue1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "prop_drop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "racetest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rampage_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "randomchar_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "range_modern");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "range_modern_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rerecord_recording");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "respawn_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "restrictedareas");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_abandonedcar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_accident");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_armybase");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_arrests");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_atmrobbery");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_bikethief");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_border");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_burials");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_bus_tours");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_cartheft");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_chasethieves");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_crashrescue");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_cultshootout");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_dealgonewrong");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_domestic");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_drunkdriver");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_duel");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_gangfight");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_gang_intimidation");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_getaway_driver");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_hitch_lift");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_homeland_security");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_lossantosintl");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_lured");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_monkey");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_mountdance");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_muggings");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_paparazzi");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_prison");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_prisonerlift");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_prisonvanbreak");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_rescuehostage");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_seaplane");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_securityvan");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_shoprobbery");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_snatched");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_stag_do");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "re_yetarian");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rollercoaster");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rural_bank_heist");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rural_bank_prep1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "rural_bank_setup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "savegame_bed");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "save_anywhere");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scaleformgraphictest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scaleformminigametest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scaleformprofiling");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scaleformtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scene_builder");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sclub_front_bouncer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scripted_cam_editor");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scriptplayground");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scripttest1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scripttest2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scripttest3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "scripttest4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "script_metrics");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sctv");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sc_lb_global_block");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "selector_example");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "selling_short_1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "selling_short_2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "shooting_camera");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "shoprobberies");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "shop_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "shot_bikejump");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "shrinkletter");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sh_intro_f_hills");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sh_intro_m_home");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "smoketest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "social_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "solomon1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "solomon2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "solomon3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "spaceshipparts");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "spawn_activities");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "speech_reverb_tracker");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "spmc_instancer");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "spmc_preloader");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sp_dlc_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sp_editor_mission_instance");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sp_menuped");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "sp_pilotschool_reg");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "standard_global_init");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "standard_global_reg");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "startup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "startup_install");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "startup_locationtest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "startup_positioning");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "startup_smoketest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stats_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stock_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "streaming");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stripclub");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stripclub_drinking");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stripclub_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stripperhome");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "stunt_plane_races");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tasklist_1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tattoo_shop");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxilauncher");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxiservice");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxitutorial");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_clowncar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_cutyouin");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_deadline");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_followcar");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_gotyounow");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_gotyourback");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_needexcitement");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_procedural");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_takeiteasy");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "taxi_taketobest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tempalpha");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "temptest");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tennis");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tennis_ambient");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tennis_family");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tennis_network_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "test_startup");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "thelastone");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "timershud");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "title_update_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tonya1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tonya2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tonya3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tonya4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tonya5");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "towing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "traffickingsettings");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "traffickingteleport");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "traffick_air");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "traffick_ground");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "train_create_widget");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "train_tester");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "trevor1");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "trevor2");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "trevor3");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "trevor4");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "triathlonsp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tunables_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "tuneables_processing");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ufo");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "ugc_global_registration");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "underwaterpickups");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "utvc");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "vehicle_ai_test");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "vehicle_force_widget");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "vehicle_gen_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "vehicle_plate");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "veh_play_widget");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "walking_ped");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "wardrobe_mp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "wardrobe_sp");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "weapon_audio_widget");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "wp_partyboombox");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "xml_menus");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "yoga");
        }
    }

    public class MasterServerList
    {
        public List<string> list { get; set; }
    }

    public class WelcomeSchema
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Picture { get; set; }
    }
}
