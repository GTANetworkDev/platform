using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTANetwork.GUI;
using GTANetworkShared;
using Lidgren.Network;
using NativeUI;
using NativeUI.PauseMenu;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;

namespace GTANetwork
{
    public class Main : Script
    {
        public static PlayerSettings PlayerSettings;
        
        public static readonly ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9;

        private readonly UIMenu _mainMenu;
        private readonly UIMenu _serverBrowserMenu;
        private readonly UIMenu _playersMenu;
        private readonly UIMenu _settingsMenu;

        public static bool BlockControls;
        public static bool WriteDebugLog;

        public static NetEntityHandler NetEntityHandler;

        private readonly MenuPool _menuPool;

        private UIResText _verionLabel = new UIResText("GTAN " + CurrentVersion.ToString(), new Point(), 0.35f, Color.FromArgb(100, 200, 200, 200));

        private string _clientIp;
        private readonly ClassicChat _chat;

        public static NetClient Client;
        private static NetPeerConfiguration _config;
        public static ParseableVersion CurrentVersion = ParseableVersion.FromAssembly();
        
        public static SynchronizationMode GlobalSyncMode;
        public static bool LerpRotaion = true;

        private static int _channel;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private bool _lastDead;
        private bool _lastKilled;
        private bool _wasTyping;

        public static TabView MainMenu;
        
        private DebugWindow _debug;
        private SyncEventWatcher Watcher;

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        //

        
        public Main()
        {
            PlayerSettings = Util.ReadSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");
            _threadJumping = new Queue<Action>();

            NetEntityHandler = new NetEntityHandler();

            Watcher = new SyncEventWatcher(this);

            Opponents = new Dictionary<long, SyncPed>();
            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            EntityCleanup = new List<int>();
            BlipCleanup = new List<int>();
            
            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++) _emptyVehicleMods.Add(i, 0);

            _chat = new ClassicChat();
            _chat.OnComplete += (sender, args) =>
            {
                var message = Chat.SanitizeString(_chat.CurrentInput);
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
                    Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 1);
                }
                _chat.IsFocused = false;
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

            MainMenuCamera = World.CreateCamera(new Vector3(743.76f, 1070.7f, 350.24f), new Vector3(),
                GameplayCamera.FieldOfView);
            MainMenuCamera.PointAt(new Vector3(707.86f, 1228.09f, 333.66f));
        }

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
        
        private int _currentServerPort;
        private string _currentServerIp;
        private bool _debugWindow;

        public static Dictionary<long, SyncPed> Opponents;
        public static Dictionary<string, SyncPed> Npcs;
        public static float Latency;
        private int Port = 4499;

        public static Camera MainMenuCamera;

        private void AddToFavorites(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            int port;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out port)) return;
            PlayerSettings.FavoriteServers.Add(server);
            Util.SaveSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");
        }

        private void RemoveFromFavorites(string server)
        {
            PlayerSettings.FavoriteServers.Remove(server);
            Util.SaveSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");
        }

        private void SaveSettings()
        {
            Util.SaveSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");
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
                Util.SaveSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");

                var item = new UIMenuItem(server.Text);
                item.Description = server.Description;
                item.SetRightLabel(server.RightLabel);
                item.SetLeftBadge(server.LeftBadge);
                item.Activated += (sender, selectedItem) =>
                        {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers.");

                        if (Opponents != null)
                        {
                            Opponents.ToList().ForEach(pair => pair.Value.Clear());
                            Opponents.Clear();
                        }

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
                Util.SaveSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");

                var item = new UIMenuItem(server);
                item.Description = server;
                item.SetRightLabel(server);
                item.Activated += (sender, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers.");

                        if (Opponents != null)
                        {
                            Opponents.ToList().ForEach(pair => pair.Value.Clear());
                            Opponents.Clear();
                        }

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
                    if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                        return;
                    string response = String.Empty;
                    try
                    {
                        using (var wc = new ImpatientWebClient())
                        {
                            response = wc.DownloadString(PlayerSettings.MasterServerAddress);
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
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(response))
                        return;

                    var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

                    if (dejson == null) return;

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

                    var list = new List<string>();

                    list.AddRange(dejson.list);

                    foreach (var server in PlayerSettings.FavoriteServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

                    foreach (var server in PlayerSettings.RecentServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

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
                        if (_serverBrowser.Items.Count > 0)
                            lastIndx = _serverBrowser.Index;

                        _serverBrowser.Items.Add(item);
                        _serverBrowser.Index = lastIndx;

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

                    _currentOnlineServers = list.Count;

                    Client.DiscoverLocalPeers(Port);

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
                            DownloadManager.Log("DISCOVERY EXCEPTION " + e.Message + " " + e.StackTrace);
                        }
                    }
                }
                catch (Exception e)
                {
                    DownloadManager.Log("DISCOVERY CRASH: " + e.Message + " " + e.StackTrace);
                }
            });

            fetchThread.Start();
        }
        
        private void RebuildPlayersList()
        {
            _serverPlayers.Dictionary.Clear();

            List<SyncPed> list = null;
            lock (Opponents)
            {
                if (Opponents == null) return;

                list = new List<SyncPed>(Opponents.Select(pair => pair.Value));
            }
            
            _serverPlayers.Dictionary.Add(PlayerSettings.DisplayName, ((int)(Latency * 1000)) + "ms");

            foreach (var ped in list)
            {
                _serverPlayers.Dictionary.Add(ped.Name == null ? "<Unknown>" : ped.Name, ((int)(ped.Latency * 1000)) + "ms");
            }
        }

        private void TickSpinner()
        {
            OnTick(null, EventArgs.Empty);
        }

        private TabMapItem _mainMapItem;
        private TabTextItem _welcomePage;
        private void BuildMainMenu()
        {
            MainMenu = new TabView("Grand Theft Auto Network");
            MainMenu.CanLeave = false;
            MainMenu.MoneySubtitle = "GTAN " + ParseableVersion.FromAssembly().ToString();

            _mainMapItem = new TabMapItem();

            #region Welcome Screen
            {
                _welcomePage = new TabTextItem("Welcome", "Welcome to GTA Network", "Join a server on the right! Weekly Updates! Donate, or whatever.");
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

                                        if (Opponents != null)
                                        {
                                            Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                            Opponents.Clear();
                                        }

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

                                        if (Opponents != null)
                                        {
                                            Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                            Opponents.Clear();
                                        }

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
                    var debugItem = new UIMenuCheckboxItem("Write Debug Info To File", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        WriteDebugLog = @checked;
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

                var localServs = new TabItemSimpleList("Audio", new Dictionary<string, string>
                {
                    { "Name", "Guadmaz"},
                    { "Chat size", "0/10"}
                });

                var favServers = new TabItemSimpleList("Video", new Dictionary<string, string>
                {
                    { "Name", "Guadmaz"},
                    { "Chat size", "0/10"}
                });

                var recentServs = new TabItemSimpleList("Keybindings", new Dictionary<string, string>
                {
                    { "Name", "Guadmaz"},
                    { "Chat size", "0/10"}
                });

                var welcomeItem = new TabSubmenuItem("settings", new List<TabItem>() { internetServers, localServs, favServers, recentServs });
                MainMenu.AddTab(welcomeItem);
            }

            #endregion
            
            #region About
            {
                var welcomeItem = new TabTextItem("about", "About", "Author: Guadmaz");
                MainMenu.Tabs.Add(welcomeItem);
            }
            #endregion

            #region Quit
            {
                var welcomeItem = new TabTextItem("Quit", "Quit GTA Network", "Are you sure you want to quit Grand Theft Auto Network and return to desktop?");
                welcomeItem.CanBeFocused = false;
                welcomeItem.Activated += (sender, args) =>
                {
                    MainMenu.Visible = false;
                    World.RenderingCamera = null;
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

                        if (Opponents != null)
                        {
                            Opponents.ToList().ForEach(pair => pair.Value.Clear());
                            Opponents.Clear();
                        }

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
                if (Client != null) Client.Disconnect("Connection closed by peer.");
            };

            _serverItem = new TabSubmenuItem("server", new List<TabItem>() { _serverPlayers, favTab, dcItem });
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

        public static void AddMap(ServerMap map)
        {

            if (map.Objects != null)
                foreach (var pair in map.Objects)
                {
                    var ourVeh = NetEntityHandler.CreateObject(new Model(pair.Value.ModelHash), pair.Value.Position.ToVector(),
                        pair.Value.Rotation.ToVector(), false, pair.Key); // TODO: Make dynamic props work
                }

            if (map.Vehicles != null)
                foreach (var pair in map.Vehicles)
                {
                    var ourVeh = NetEntityHandler.CreateVehicle(new Model(pair.Value.ModelHash), pair.Value.Position.ToVector(),
                        pair.Value.Rotation.ToVector(), pair.Key);
                    ourVeh.PrimaryColor = (VehicleColor)pair.Value.PrimaryColor;
                    ourVeh.SecondaryColor = (VehicleColor)pair.Value.SecondaryColor;
                    ourVeh.PearlescentColor = (VehicleColor) 0;
                    ourVeh.RimColor = (VehicleColor)0;
                    ourVeh.EngineHealth = pair.Value.Health;
                    ourVeh.SirenActive = pair.Value.Siren;
                    
                    for (int i = 0; i < pair.Value.Doors.Length; i++)
                    {
                        if (pair.Value.Doors[i])
                            ourVeh.OpenDoor((VehicleDoor) i, false, true);
                        else ourVeh.CloseDoor((VehicleDoor) i, true);
                    }

                    for (int i = 0; i < pair.Value.Tires.Length; i++)
                    {
                        //Util.SafeNotify("TIRE #" + i + " is burst? " + pair.Value.Tires[i]);
                        if (pair.Value.Tires[i])
                        {
                            ourVeh.IsInvincible = false;
                            ourVeh.BurstTire(i);
                        }
                    }

                    if (pair.Value.Trailer != 0)
                    {
                        var trailerId = NetEntityHandler.NetToEntity(pair.Value.Trailer);
                        if (trailerId != null)
                        {
                            Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, ourVeh, trailerId, 2f);
                        }
                    }

                    Function.Call(Hash.SET_VEHICLE_MOD_KIT, ourVeh, 0);

                    for (int i = 0; i < pair.Value.Mods.Length; i++) 
                    {
                        ourVeh.SetMod((VehicleMod) i, pair.Value.Mods[i], false);
                    }

                    if (pair.Value.IsDead)
                    {
                        ourVeh.IsInvincible = false;
                        ourVeh.Explode();
                    }
                    else
                        ourVeh.IsInvincible = true;
                }

            if (map.Blips != null)
            {
                foreach (var blip in map.Blips)
                {
                    var ourBlip = NetEntityHandler.CreateBlip(blip.Value.Position.ToVector(), blip.Key);
                    if (blip.Value.Sprite != 0)
                        ourBlip.Sprite = (BlipSprite) blip.Value.Sprite;
                    ourBlip.Color = (BlipColor)blip.Value.Color;
                    ourBlip.Alpha = blip.Value.Alpha;
                    ourBlip.IsShortRange = blip.Value.IsShortRange;
                    ourBlip.Scale = blip.Value.Scale;
                }
            }

            if (map.Markers != null)
            {
                foreach (var marker in map.Markers)
                {
                    NetEntityHandler.CreateMarker(marker.Value.MarkerType, marker.Value.Position, marker.Value.Rotation,
                        marker.Value.Direction, marker.Value.Scale, marker.Value.Red, marker.Value.Green,
                        marker.Value.Blue, marker.Value.Alpha, marker.Key);
                }
            }

            if (map.Pickups != null)
            {
                foreach (var pickup in map.Pickups)
                {
                    NetEntityHandler.CreatePickup(pickup.Value.Position.ToVector(), pickup.Value.Rotation.ToVector(),
                        pickup.Value.ModelHash, pickup.Value.Amount, pickup.Key);
                }
            }
        }

        public static void StartClientsideScripts(ScriptCollection scripts)
        {
            if (scripts.ClientsideScripts != null)
                foreach (var scr in scripts.ClientsideScripts)
                {
                    JavascriptHook.StartScript(scr);
                }
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

        public static void SendPlayerData()
        {
            var player = Game.Player.Character;
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.VehicleHandle = NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PlayerHealth = (int)(100 * (player.Health / (float)player.MaxHealth));
                obj.VehicleHealth = veh.EngineHealth;
                obj.VehicleSeat = Util.GetPedSeat(player);
                obj.IsPressingHorn = Game.Player.IsPressingHorn;
                obj.IsSirenActive = veh.SirenActive;
                obj.Speed = veh.Speed;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = player.Armor;
                obj.IsVehicleDead = veh.IsDead;

                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.WeaponHash = GetCurrentVehicleWeaponHash(Game.Player.Character);
                    obj.IsShooting = Game.IsControlPressed(0, Control.VehicleFlyAttack);
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.IsShooting = Game.IsControlPressed(0, Control.VehicleAttack);
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();
                }
                else
                {
                    obj.IsShooting = Game.IsControlPressed(0, Control.Attack);
                    //obj.IsShooting = Game.Player.Character.IsShooting;
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();
                }


                var bin = SerializeBinary(obj);

                var msg = Client.CreateMessage();
                msg.Write((int)PacketType.VehiclePositionData);
                msg.Write(bin.Length);
                msg.Write(bin);
                try
                {
                    Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, 1);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    DownloadManager.Log("FAILED TO SEND DATA: " + ex.Message);
                    DownloadManager.Log(ex.StackTrace);
                }
                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
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
                obj.PedArmor = player.Armor;
                obj.IsRagdoll = player.IsRagdoll;
                obj.IsFreefallingWithChute =
                        Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                        Game.Player.Character.IsInAir;
                obj.IsInMeleeCombat = player.IsInMeleeCombat;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = (int)(100 * (player.Health / (float)player.MaxHealth));
                obj.IsAiming = aiming;
                obj.IsShooting = shooting || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack));
                obj.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);
                obj.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2;
                obj.Speed = player.Velocity.Length();

                //obj.PedProps = CheckPlayerProps();

                var bin = SerializeBinary(obj);

                var msg = Client.CreateMessage();

                msg.Write((int)PacketType.PedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                try
                {
                    Client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, 1);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    DownloadManager.Log("FAILED TO SEND DATA: " + ex.Message);
                    DownloadManager.Log(ex.StackTrace);
                }

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }
        /*
        public static void SendPedData(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                var veh = ped.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                //obj.Quaternion = veh.Quaternion.ToLQuaternion();
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = ped.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PrimaryColor = (int)veh.PrimaryColor;
                obj.SecondaryColor = (int)veh.SecondaryColor;
                obj.PlayerHealth = ped.Health;
                obj.VehicleHealth = veh.Health;
                obj.VehicleSeat = Util.GetPedSeat(ped);
                obj.Name = ped.Handle.ToString();
                obj.Speed = veh.Speed;
                obj.IsSirenActive = veh.SirenActive;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.NpcVehPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, ped.Handle);

                Vector3 aimCoord = new Vector3();
                if (shooting)
                    aimCoord = Util.GetLastWeaponImpact(ped);

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = ped.Position.ToLVector();
                //obj.Quaternion = ped.Quaternion.ToLQuaternion();
                obj.Quaternion = ped.Rotation.ToLVector();

                obj.PedModelHash = ped.Model.Hash;
                obj.WeaponHash = (int)ped.Weapons.Current.Hash;
                obj.PlayerHealth = ped.Health;
                obj.Name = ped.Handle.ToString();
                obj.IsAiming = false;
                obj.IsShooting = shooting;
                obj.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, ped.Handle);
                obj.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, ped.Handle) == 2;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();

                msg.Write((int)PacketType.NpcPedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }
        */
        
        public static void InvokeFinishedDownload()
        {
            var confirmObj = Client.CreateMessage();
            confirmObj.Write((int)PacketType.ConnectionConfirmed);
            confirmObj.Write(true);
            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered);
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

        private int _debugStep;

        private int DEBUG_STEP
        {
            get { return _debugStep; }
            set
            {
                _debugStep = value;
                //UI.ShowSubtitle(_debugStep.ToString());
            }
        }

        private int _debugPickup;

        public void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;

            if (!_hasInitialized)
            {
                RebuildServerBrowser();
                _hasInitialized = true;
            }

            DEBUG_STEP = 0;

            Game.DisableControl(0, Control.FrontendPauseAlternate);
            
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
            }
            DEBUG_STEP = 1;

            if (!MainMenu.Visible || MainMenu.TemporarilyHidden)
                _chat.Tick();
            
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
            }

            if (Game.IsKeyPressed(Keys.NumPad7))
            {
                offset = new Vector3(offset.X, offset.Y, offset.Z + 0.01f);
            }

            if (Game.IsKeyPressed(Keys.NumPad1))
            {
                offset = new Vector3(offset.X, offset.Y, offset.Z - 0.01f);
            }

            if (Game.IsKeyPressed(Keys.NumPad4))
            {
                offset = new Vector3(offset.X, offset.Y - 0.01f, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad6))
            {
                offset = new Vector3(offset.X, offset.Y + 0.01f, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad2))
            {
                offset = new Vector3(offset.X - 0.01f, offset.Y, offset.Z);
            }

            if (Game.IsKeyPressed(Keys.NumPad8))
            {
                offset = new Vector3(offset.X + 0.01f, offset.Y, offset.Z);
            }


            if (Game.IsControlJustPressed(0, Control.Context))
            {
                var p = Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0f));
                //_debugPickup = Function.Call<int>(Hash.CREATE_PICKUP, 1295434569, p.X, p.Y, p.Z, 0, 1, 1, 0);
                _debugPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, 1295434569, p.X, p.Y, p.Z, 0, 0, 0, 0, 1, 0, false, 0);
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

            */



            DEBUG_STEP = 3;
#if DEBUG
            if (display)
            {
                Debug();
            }
            DEBUG_STEP = 4;
            if (_debugWindow)
            {
                _debug.Visible = true;
                _debug.Draw();
            }

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
            */
            

#endif
            DEBUG_STEP = 5;
            ProcessMessages();
            DEBUG_STEP = 6;

            if (Client == null || Client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                Client.ConnectionStatus == NetConnectionStatus.None) return;
            var res = UIMenu.GetScreenResolutionMantainRatio();
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
                if (playerCar != null) playerCar.IsInvincible = false;
            }
            DEBUG_STEP = 11;
            _lastPlayerCar = playerCar;

            Game.DisableControl(0, Control.SpecialAbility);
            Game.DisableControl(0, Control.SpecialAbilityPC);
            Game.DisableControl(0, Control.SpecialAbilitySecondary);
            Game.DisableControl(0, Control.CharacterWheel);
            Game.DisableControl(0, Control.Phone);
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
                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
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

                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
            }
            DEBUG_STEP = 20;
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
            DEBUG_STEP = 21;
            _lastKilled = killed;

            Function.Call(Hash.SET_RANDOM_TRAINS, 0);
            DEBUG_STEP = 22;
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);

            Function.Call((Hash) 0x2F9A292AD0A3BD89);
            Function.Call((Hash) 0x5F3B7749C112D552);

            DEBUG_STEP = 23;
            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character) == 2)
            {
                Game.DisableControl(0, Control.Aim);
                Game.DisableControl(0, Control.Attack);
            }
            DEBUG_STEP = 24;
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
                    if (!NetEntityHandler.ContainsLocalHandle(entity.Handle))
                        entity.Delete();
                }
            }
            DEBUG_STEP = 25;
            _whoseturnisitanyways = !_whoseturnisitanyways;
            
            /*string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                _bytesSent / 1000, _messagesReceived, _messagesSent);
                */
            //UI.ShowSubtitle(stats);

            lock (_threadJumping)
            {
                if (_threadJumping.Any())
                {
                    Action action = _threadJumping.Dequeue();
                    if (action != null) action.Invoke();
                }
            }
            DEBUG_STEP = 26;
            Dictionary<string, NativeData> tickNatives = null;
            lock (_tickNatives) tickNatives = new Dictionary<string,NativeData>(_tickNatives);
            DEBUG_STEP = 27;
            for (int i = 0; i < tickNatives.Count; i++) DecodeNativeCall(tickNatives.ElementAt(i).Value);
            DEBUG_STEP = 28;
        }

        public static bool IsOnServer()
        {
            return Client != null && Client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            _chat.OnKeyDown(e.KeyCode);
            if (e.KeyCode == Keys.F10 && !_chat.IsFocused)
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

            if (e.KeyCode == Keys.G && !Game.Player.Character.IsInVehicle() && IsOnServer() && !_chat.IsFocused)
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

                    if (vehs[0].PassengerSeats == 2) seat = VehicleSeat.Passenger;

                    if (vehs[0].PassengerSeats > 4 && vehs[0].GetPedOnSeat(seat).Handle != 0)
                    {
                        if (seat == VehicleSeat.LeftRear)
                        {
                            for (int i = 3; i < vehs[0].PassengerSeats; i += 2)
                            {
                                if (vehs[0].GetPedOnSeat((VehicleSeat) i).Handle == 0)
                                {
                                    seat = (VehicleSeat) i;
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

                    if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash) vehs[0].Model.Hash, 0, true) && Game.Player.Character.IsIdle && !Game.Player.IsAiming)
                        Game.Player.Character.SetIntoVehicle(vehs[0], seat);
                    else
                        Game.Player.Character.Task.EnterVehicle(vehs[0], seat, -1, 2f);
                    _isGoingToCar = true;
                }
            }

            if (e.KeyCode == Keys.T && IsOnServer())
            {
                if (!_oldChat)
                {
                    _chat.IsFocused = true;
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
                        Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
                    }
                }
            }
        }

        public void ConnectToServer(string ip, int port = 0)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            _chat.Init();

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

            lock (Opponents) Opponents = new Dictionary<long, SyncPed>();
            lock (Npcs) Npcs = new Dictionary<string, SyncPed>();
            lock (_tickNatives) _tickNatives = new Dictionary<string, NativeData>();

            var msg = Client.CreateMessage();

            var obj = new ConnectionRequest();
            obj.SocialClubName = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Unknown" : Game.Player.Name; // To be used as identifiers in server files
            obj.DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.DisplayName) ? obj.SocialClubName : PlayerSettings.DisplayName.Trim();
            if (!string.IsNullOrEmpty(_password)) obj.Password = _password;
            obj.ScriptVersion = (byte)LocalScriptVersion;
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

            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "blip_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "event_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cheat_controller");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "restrictedAreas");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "vehicle_gen_controller");
            
            _currentServerIp = ip;
            _currentServerPort = port == 0 ? Port : port;
        }

        public void ProcessMessages()
        {
            NetIncomingMessage msg;
            while (Client != null && (msg = Client.ReadMessage()) != null)
            {
                PacketType type = PacketType.WorldSharingStop;
                DownloadManager.Log("RECEIVED MESSAGE " + msg.MessageType);
                try
                {
                    _messagesReceived++;
                    _bytesReceived += msg.LengthBytes;

                    if (msg.MessageType == NetIncomingMessageType.Data)
                    {
                        type = (PacketType) msg.ReadInt32();
                        DownloadManager.Log("RECEIVED DATATYPE " + type);
                        switch (type)
                        {
                            case PacketType.VehiclePositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;
                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            //data.Quaternion.ToQuaternion());
                                            data.Quaternion.ToVector());
                                        Opponents.Add(data.Id, repr);
                                    }
                                    if (Opponents[data.Id].Character != null)
                                        NetEntityHandler.SetEntity(data.NetHandle, Opponents[data.Id].Character.Handle);
                                    Opponents[data.Id].VehicleNetHandle = data.VehicleHandle;
                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].LastUpdateReceived = DateTime.Now;
                                    Opponents[data.Id].VehiclePosition =
                                        data.Position.ToVector();
                                    Opponents[data.Id].VehicleVelocity = data.Velocity.ToVector();
                                    Opponents[data.Id].IsVehDead = data.IsVehicleDead;
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].VehicleHash =
                                        data.VehicleModelHash;
                                    Opponents[data.Id].PedArmor = data.PedArmor;
                                    Opponents[data.Id].VehicleRotation =
                                        //data.Quaternion.ToQuaternion();
                                        data.Quaternion.ToVector();
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].VehicleHealth = data.VehicleHealth;
                                    //Opponents[data.Id].VehiclePrimaryColor = data.PrimaryColor;
                                    //Opponents[data.Id].VehicleSecondaryColor = data.SecondaryColor;
                                    Opponents[data.Id].VehicleSeat = data.VehicleSeat;
                                    Opponents[data.Id].IsInVehicle = true;
                                    Opponents[data.Id].Latency = data.Latency;

                                    //Opponents[data.Id].VehicleMods = data.VehicleMods;
                                    Opponents[data.Id].IsHornPressed = data.IsPressingHorn;
                                    Opponents[data.Id].Speed = data.Speed;
                                    Opponents[data.Id].Siren = data.IsSirenActive;
                                    Opponents[data.Id].IsShooting = data.IsShooting;
                                    Opponents[data.Id].CurrentWeapon = data.WeaponHash;
                                    if (data.AimCoords != null)
                                        Opponents[data.Id].AimCoords = data.AimCoords.ToVector();
                                }
                            }
                                break;
                            case PacketType.PedPositionData:
                            {

                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;
                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToVector());
                                        Opponents.Add(data.Id, repr);
                                    }

                                    if (Opponents[data.Id].Character != null)
                                        NetEntityHandler.SetEntity(data.NetHandle, Opponents[data.Id].Character.Handle);
                                    Opponents[data.Id].Speed = data.Speed;
                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].IsRagdoll = data.IsRagdoll;
                                    Opponents[data.Id].PedArmor = data.PedArmor;
                                    Opponents[data.Id].LastUpdateReceived = DateTime.Now;
                                    Opponents[data.Id].Position = data.Position.ToVector();
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].IsInMeleeCombat = data.IsInMeleeCombat;
                                    Opponents[data.Id].Rotation = data.Quaternion.ToVector();
                                    Opponents[data.Id].IsFreefallingWithParachute = data.IsFreefallingWithChute;
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].IsInVehicle = false;
                                    Opponents[data.Id].AimCoords = data.AimCoords.ToVector();
                                    Opponents[data.Id].CurrentWeapon = data.WeaponHash;
                                    Opponents[data.Id].IsAiming = data.IsAiming;
                                    Opponents[data.Id].IsJumping = data.IsJumping;
                                    Opponents[data.Id].IsShooting = data.IsShooting;
                                    Opponents[data.Id].Latency = data.Latency;
                                    Opponents[data.Id].IsParachuteOpen = data.IsParachuteOpen;
                                }
                            }
                                break;
                            case PacketType.NpcVehPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;

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
                                }
                            }
                                break;
                            case PacketType.NpcPedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

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
                                }
                            }
                                break;
                            case PacketType.CreateEntity:
                            {
                                var len = msg.ReadInt32();
                                DownloadManager.Log("Received CreateEntity");
                                var data = DeserializeBinary<CreateEntity>(msg.ReadBytes(len)) as CreateEntity;
                                if (data != null && data.Properties != null)
                                {
                                    DownloadManager.Log("CreateEntity was not null. Type: " + data.EntityType);
                                    DownloadManager.Log("Model: " + data.Properties.ModelHash);
                                    if (data.EntityType == (byte) EntityType.Vehicle)
                                    {
                                        var prop = (VehicleProperties) data.Properties;
                                        var veh = NetEntityHandler.CreateVehicle(new Model(data.Properties.ModelHash),
                                            data.Properties.Position?.ToVector() ?? new Vector3(),
                                            data.Properties.Rotation?.ToVector() ?? new Vector3(), data.NetHandle);
                                        DownloadManager.Log("Settings vehicle color 1");
                                        veh.PrimaryColor = (VehicleColor) prop.PrimaryColor;
                                        DownloadManager.Log("Settings vehicle color 2");
                                        veh.PrimaryColor = (VehicleColor) prop.SecondaryColor;
                                        DownloadManager.Log("Settings vehicle extra colors");
                                        Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, veh, 0, 0);
                                        DownloadManager.Log("CreateEntity done");
                                    }
                                    else if (data.EntityType == (byte) EntityType.Prop)
                                    {
                                        DownloadManager.Log("It was a prop. Spawning...");
                                        NetEntityHandler.CreateObject(new Model(data.Properties.ModelHash),
                                            data.Properties.Position?.ToVector() ?? new Vector3(),
                                            data.Properties.Rotation?.ToVector() ?? new Vector3(), false, data.NetHandle);
                                    }
                                    else if (data.EntityType == (byte) EntityType.Blip)
                                    {
                                        NetEntityHandler.CreateBlip(data.Properties.Position.ToVector(), data.NetHandle);
                                    }
                                    else if (data.EntityType == (byte) EntityType.Marker)
                                    {
                                        var prop = (MarkerProperties) data.Properties;
                                        NetEntityHandler.CreateMarker(prop.MarkerType, prop.Position, prop.Rotation,
                                            prop.Direction, prop.Scale, prop.Red, prop.Green, prop.Blue, prop.Alpha,
                                            data.NetHandle);
                                    }
                                    else if (data.EntityType == (byte) EntityType.Pickup)
                                    {
                                        var amount = ((PickupProperties) data.Properties).Amount;
                                        NetEntityHandler.CreatePickup(data.Properties.Position.ToVector(),
                                            data.Properties.Rotation.ToVector(), data.Properties.ModelHash, amount,
                                            data.NetHandle);
                                    }
                                }
                            }
                                break;
                            case PacketType.UpdateMarkerProperties:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<CreateEntity>(msg.ReadBytes(len)) as CreateEntity;
                                if (data != null && data.Properties != null)
                                {
                                    if (data.EntityType == (byte) EntityType.Marker &&
                                        NetEntityHandler.Markers.ContainsKey(data.NetHandle))
                                    {
                                        var prop = (MarkerProperties) data.Properties;
                                        NetEntityHandler.Markers[data.NetHandle] = new MarkerProperties()
                                        {
                                            MarkerType = prop.MarkerType,
                                            Alpha = prop.Alpha,
                                            Blue = prop.Blue,
                                            Direction = prop.Direction,
                                            Green = prop.Green,
                                            Position = prop.Position,
                                            Red = prop.Red,
                                            Rotation = prop.Rotation,
                                            Scale = prop.Scale,
                                        };
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
                                    DownloadManager.Log("RECEIVED DELETE ENTITY " + data.NetHandle);
                                    if (NetEntityHandler.Markers.ContainsKey(data.NetHandle))
                                    {
                                        NetEntityHandler.Markers.Remove(data.NetHandle);
                                    }
                                    else
                                    {
                                        var entity = NetEntityHandler.NetToEntity(data.NetHandle);
                                        if (entity != null)
                                        {
                                            if (NetEntityHandler.IsBlip(entity.Handle))
                                            {
                                                if (new Blip(entity.Handle).Exists())
                                                    new Blip(entity.Handle).Remove();
                                            }
                                            else if (NetEntityHandler.IsPickup(entity.Handle))
                                            {
                                                Function.Call(Hash.REMOVE_PICKUP, entity.Handle);
                                            }
                                            else
                                            {
                                                entity.Delete();
                                            }
                                            NetEntityHandler.RemoveByNetHandle(data.NetHandle);
                                        }
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
                                        (FileType) data.FileType, data.Length, data.Md5Hash);
                                    var newMsg = Client.CreateMessage();
                                    newMsg.Write((int)PacketType.FileAcceptDeny);
                                    newMsg.Write(data.Id);
                                    newMsg.Write(acceptDownload);
                                    Client.SendMessage(newMsg, NetDeliveryMethod.ReliableOrdered, 29);
                                }
                                else
                                {
                                    DownloadManager.Log("DATA WAS NULL ON REQUEST");
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
                                    _chat.AddMessage(data.Sender, data.Message);
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
                                    DownloadManager.Log("RECEIVED SYNC EVENT " + ((SyncEventType)data.EventType) + ": " + args.Aggregate((f, s) => f.ToString() + ", " + s.ToString()));
                                    switch ((SyncEventType) data.EventType)
                                    {
                                        case SyncEventType.LandingGearChange:
                                        {
                                            var veh = NetEntityHandler.NetToEntity((int) args[0]);
                                            var newState = (int) args[1];
                                            if (veh == null) return;
                                            Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, veh, newState);
                                        }
                                            break;
                                        case SyncEventType.DoorStateChange:
                                        {
                                            var veh = NetEntityHandler.NetToEntity((int) args[0]);
                                            var doorId = (int) args[1];
                                            var newFloat = (bool) args[2];
                                            if (veh == null) return;
                                            if (newFloat)
                                                new Vehicle(veh.Handle).OpenDoor((VehicleDoor) doorId, false, false);
                                            else
                                                new Vehicle(veh.Handle).CloseDoor((VehicleDoor) doorId, false);
                                        }
                                            break;
                                        case SyncEventType.BooleanLights:
                                        {
                                            var veh = NetEntityHandler.NetToEntity((int) args[0]);
                                            var lightId = (Lights) (int) args[1];
                                            var state = (bool) args[2];
                                            if (veh == null) return;
                                            if (lightId == Lights.NormalLights)
                                                new Vehicle(veh.Handle).LightsOn = state;
                                            else if (lightId == Lights.Highbeams)
                                                Function.Call(Hash.SET_VEHICLE_FULLBEAM, veh.Handle, state);
                                        }
                                            break;
                                        case SyncEventType.TrailerDeTach:
                                        {
                                            var newState = (bool) args[0];
                                            if (!newState)
                                            {
                                                var car = NetEntityHandler.NetToEntity((int) args[1]);
                                                if (car != null)
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, car.Handle);
                                            }
                                            else
                                            {
                                                var car = NetEntityHandler.NetToEntity((int) args[1]);
                                                var trailer = NetEntityHandler.NetToEntity((int) args[2]);
                                                if (car != null && trailer != null)
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, car, trailer, 4f);
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
                                            var newRadio = (int) args[1];
                                            if (veh != null)
                                            {
                                                var rad = (RadioStation) newRadio;
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
                                            var pickupId = NetEntityHandler.NetToEntity((int) args[0]);
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
                                lock (Opponents)
                                {
                                    if (data != null && Opponents.ContainsKey(data.Id))
                                    {
                                        Opponents[data.Id].Clear();
                                        Opponents.Remove(data.Id);

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
                                }
                            }
                                break;
                            case PacketType.ScriptEventTrigger:
                            {
                                var len = msg.ReadInt32();
                                var data =
                                    DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                                if (data != null)
                                {
                                    if (data.Arguments != null)
                                        JavascriptHook.InvokeServerEvent(data.EventName,
                                            DecodeArgumentList(data.Arguments.ToArray()).ToArray());
                                    else
                                        JavascriptHook.InvokeServerEvent(data.EventName, new object[0]);
                                }
                            }
                                break;
                            case PacketType.WorldSharingStop:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                if (data == null) return;
                                lock (Npcs)
                                {
                                    foreach (
                                        var pair in
                                            new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id)
                                                .ToList())
                                    {
                                        pair.Value.Clear();
                                        Npcs.Remove(pair.Key);
                                    }
                                }
                            }
                                break;
                            case PacketType.NativeCall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData) DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                DownloadManager.Log("RECEIVED NATIVE CALL " + data.Hash);
                                DecodeNativeCall(data);
                            }
                                break;
                            case PacketType.NativeTick:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeTickCall) DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
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
                                var data = (NativeTickCall) DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_tickNatives)
                                    if (_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Remove(data.Identifier);
                            }
                                break;
                            case PacketType.NativeOnDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData) DeserializeBinary<NativeData>(msg.ReadBytes(len));
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
                                var data = (NativeData) DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_dcNatives) if (_dcNatives.ContainsKey(data.Id)) _dcNatives.Remove(data.Id);
                            }
                                break;
                        }
                    }
                    else if (msg.MessageType == NetIncomingMessageType.ConnectionLatencyUpdated)
                    {
                        Latency = msg.ReadFloat();
                    }
                    else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                    {
                        var newStatus = (NetConnectionStatus) msg.ReadByte();
                        DownloadManager.Log("NEW STATUS: " + newStatus);
                        switch (newStatus)
                        {
                            case NetConnectionStatus.InitiatedConnect:
                                Util.SafeNotify("Connecting...");
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
                                _channel = respObj.AssignedChannel;
                                NetEntityHandler.AddEntity(respObj.CharacterHandle, Game.Player.Character.Handle);

                                var confirmObj = Client.CreateMessage();
                                confirmObj.Write((int) PacketType.ConnectionConfirmed);
                                confirmObj.Write(false);
                                Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered);
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

                                lock (Opponents)
                                {
                                    if (Opponents != null)
                                    {
                                        Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                        Opponents.Clear();
                                    }
                                }

                                lock (Npcs)
                                {
                                    if (Npcs != null)
                                    {
                                        Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                        Npcs.Clear();
                                    }
                                }

                                lock (_dcNatives)
                                    if (_dcNatives != null && _dcNatives.Any())
                                    {
                                        _dcNatives.ToList().ForEach(pair => DecodeNativeCall(pair.Value));
                                        _dcNatives.Clear();
                                    }

                                lock (_tickNatives) if (_tickNatives != null) _tickNatives.Clear();

                                lock (EntityCleanup)
                                {
                                    EntityCleanup.ForEach(ent => new Prop(ent).Delete());
                                    EntityCleanup.Clear();
                                }
                                lock (BlipCleanup)
                                {
                                    BlipCleanup.ForEach(blip => new Blip(blip).Remove());
                                    BlipCleanup.Clear();
                                }

                                _chat.Clear();
                                NetEntityHandler.ClearAll();
                                JavascriptHook.StopAllScripts();
                                DownloadManager.Cancel();
                                MainMenu.TemporarilyHidden = false;
                                JustJoinedServer = false;
                                MainMenu.Tabs.Remove(_serverItem);
                                MainMenu.Tabs.Remove(_mainMapItem);
                                if (!MainMenu.Tabs.Contains(_welcomePage))
                                    MainMenu.Tabs.Insert(0, _welcomePage);
                                MainMenu.RefreshIndex();
                                _localMarkers.Clear();
                                World.RenderingCamera = MainMenuCamera;
                                MainMenu.Visible = true;
                                break;
                        }
                    }
                    else if (msg.MessageType == NetIncomingMessageType.DiscoveryResponse)
                    {
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

                        MainMenu.Money = "Servers Online: " + _currentOnlineServers + " | Players Online: " +
                                         _currentOnlinePlayers;

                        if (data.LAN)
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

                                    if (Opponents != null)
                                    {
                                        Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                        Opponents.Clear();
                                    }

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
                            ourItem.Activated += (sender, selectedItem) =>
                            {
                                if (IsOnServer())
                                {
                                    Client.Disconnect("Switching servers.");

                                    if (Opponents != null)
                                    {
                                        Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                        Opponents.Clear();
                                    }

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


                                ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port);
                                MainMenu.TemporarilyHidden = true;
                                _connectTab.RefreshIndex();
                                AddServerToRecent(ourItem);
                            };

                            _serverBrowser.Items.Remove(ourItem);
                            _serverBrowser.Items.Insert(0, ourItem);
                            if (_serverBrowser.Focused)
                                _serverBrowser.MoveDown();
                            else
                                _serverBrowser.RefreshIndex();
                        }
                    }
                }
                catch (Exception e)
                {
                    Util.SafeNotify("Unhandled Exception ocurred in Process Messages");
                    Util.SafeNotify("Message Type: " + msg.MessageType);
                    Util.SafeNotify("Data Type: " + type);
                    Util.SafeNotify(e.Message);
                }
            }
        }


        #region debug stuff

        private DateTime _artificialLagCounter = DateTime.MinValue;
        private bool _debugStarted;
        private SyncPed _debugSyncPed;
        private int _debugPing = 30;
        private int _debugFluctuation = 0;
        private Random _r = new Random();
        private void Debug()
        {
            var player = Game.Player.Character;

            foreach (var blip in World.GetActiveBlips())
            {
                if (!NetEntityHandler.ContainsLocalHandle(blip.Handle)) blip.Remove();
            }


            if (_debugSyncPed == null)
            {
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Rotation, false);
                _debugSyncPed.Debug = true;
            }

            if (DateTime.Now.Subtract(_artificialLagCounter).TotalMilliseconds >= (_debugPing + _debugFluctuation))
            {
                _artificialLagCounter = DateTime.Now;
                _debugFluctuation = _r.Next(10) - 5;
                if (player.IsInVehicle())
                {
                    var veh = player.CurrentVehicle;
                    veh.Alpha = 50;

                    _debugSyncPed.VehiclePosition = veh.Position;
                    _debugSyncPed.VehicleRotation = veh.Rotation;
                    _debugSyncPed.VehicleVelocity = veh.Velocity;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.VehicleHash = veh.Model.Hash;
                    _debugSyncPed.VehiclePrimaryColor = (int)veh.PrimaryColor;
                    _debugSyncPed.VehicleSecondaryColor = (int)veh.SecondaryColor;
                    _debugSyncPed.PedHealth = (int)(100 * (player.Health / (float)player.MaxHealth));
                    _debugSyncPed.VehicleHealth = veh.Health;
                    _debugSyncPed.VehicleSeat = Util.GetPedSeat(player);
                    _debugSyncPed.IsHornPressed = Game.Player.IsPressingHorn;
                    _debugSyncPed.Siren = veh.SirenActive;
                    _debugSyncPed.VehicleMods = CheckPlayerVehicleMods();
                    _debugSyncPed.Speed = veh.Speed;
                    _debugSyncPed.IsInVehicle = true;
                    _debugSyncPed.LastUpdateReceived = DateTime.Now;
                    _debugSyncPed.PedArmor = player.Armor;

                    if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                    {
                        _debugSyncPed.CurrentWeapon = GetCurrentVehicleWeaponHash(Game.Player.Character);
                        _debugSyncPed.IsShooting = Game.IsControlPressed(0, Control.VehicleFlyAttack);
                    }
                    else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                    {
                        _debugSyncPed.IsShooting = Game.IsControlPressed(0, Control.VehicleAttack);
                        _debugSyncPed.AimCoords = RaycastEverything(new Vector2(0, 0));
                    }
                    else
                    {
                        //_debugSyncPed.IsShooting = Game.IsControlPressed(0, Control.Attack);
                        _debugSyncPed.IsShooting = Game.Player.Character.IsShooting;
                        _debugSyncPed.CurrentWeapon = (int)Game.Player.Character.Weapons.Current.Hash;
                        _debugSyncPed.AimCoords = RaycastEverything(new Vector2(0, 0));
                    }
                }
                else
                {
                    bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                    bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                    Vector3 aimCoord = new Vector3();
                    if (aiming || shooting)
                        aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation,
                            new Vector2(0, 0));

                    _debugSyncPed.IsInMeleeCombat = player.IsInMeleeCombat;
                    _debugSyncPed.IsRagdoll = player.IsRagdoll;
                    _debugSyncPed.PedVelocity = player.Velocity;
                    _debugSyncPed.PedHealth = player.Health;
                    _debugSyncPed.AimCoords = aimCoord;
                    _debugSyncPed.IsFreefallingWithParachute =
                        Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                        Game.Player.Character.IsInAir;
                    _debugSyncPed.PedArmor = player.Armor;
                    _debugSyncPed.Speed = player.Velocity.Length();
                    _debugSyncPed.Position = player.Position + new Vector3(1f, 0, 0);
                    _debugSyncPed.Rotation = player.Rotation;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.CurrentWeapon = (int)player.Weapons.Current.Hash;
                    _debugSyncPed.PedHealth = (int)(100 * (player.Health / (float)player.MaxHealth));
                    _debugSyncPed.IsAiming = aiming;
                    _debugSyncPed.IsShooting = shooting || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack));
                    //_debugSyncPed.IsInCover = player.IsInCover();
                    _debugSyncPed.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);
                    _debugSyncPed.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2;
                    _debugSyncPed.IsInVehicle = false;
                    _debugSyncPed.PedProps = CheckPlayerProps();
                    _debugSyncPed.LastUpdateReceived = DateTime.Now;
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
                else if (arg is OpponentPedHandleArgument)
                {
                    var handle = ((OpponentPedHandleArgument)arg).Data;
                    lock (Opponents) if (Opponents.ContainsKey(handle) && Opponents[handle].Character != null) list.Add(Opponents[handle].Character.Handle);
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
                    list.Add(NetEntityHandler.NetToEntity(((EntityArgument)arg).NetHandle));
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

        public static void SendToServer(object newData, PacketType packetType, bool important, int sequenceChannel = -1)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Client.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Client.SendMessage(msg, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced,
                sequenceChannel == -1 ? _channel : sequenceChannel);
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

            list.AddRange(DecodeArgumentList(obj.Arguments.ToArray()).Select(ob => ob is OutputArgument ? (OutputArgument)ob : new InputArgument(ob)));

            DownloadManager.Log("NATIVE CALL ARGUMENTS: " + list.Aggregate((f, s) => f + ", " + s));
            DownloadManager.Log("RETURN TYPE: " + obj.ReturnType);
            var nativeType = CheckNativeHash(obj.Hash);
            DownloadManager.Log("NATIVE TYPE IS " + nativeType);

            if ((int)nativeType >= 2)
            {
                Model model = null;
                if ((int) nativeType >= 3)
                {
                    var modelObj = obj.Arguments[(int) nativeType - 3];
                    int modelHash = 0;

                    if (modelObj is UIntArgument)
                    {
                        modelHash = unchecked((int) ((UIntArgument) modelObj).Data);
                    }
                    else if (modelObj is IntArgument)
                    {
                        modelHash = ((IntArgument) modelObj).Data;
                    }
                    model = new Model(modelHash);

                    if (model.IsValid)
                    {
                        model.Request(10000);
                    }
                }

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
            ReturnsBlip = 1,
            ReturnsEntity = 2,
            ReturnsEntityNeedsModel1 = 3,
            ReturnsEntityNeedsModel2 = 4,
            ReturnsEntityNeedsModel3 = 5,
        }

        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                    break;
                case 0xD49F9B0955C367DE:
                    return NativeType.ReturnsEntityNeedsModel2;
                case 0x7DD959874C1FD534:
                    return NativeType.ReturnsEntityNeedsModel3;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return NativeType.ReturnsEntityNeedsModel1;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return NativeType.ReturnsEntity;
                    break;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
                    break;
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
            var startingAtPort = 5000;
            var maxNumberOfPortsToCheck = 500;
            var range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            var portsInUse =
                from p in range
                join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            on p equals used.Port
                select p;

            return range.Except(portsInUse).FirstOrDefault();
        }
    }

    public class MasterServerList
    {
        public List<string> list { get; set; }
    }
}
