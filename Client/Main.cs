#define ATTACHSERVER
//#define INTEGRITYCHECK

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
using GTANetwork.Sync;
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
    internal class MessagePump : Script
    {
        public MessagePump()
        {
            Tick += (sender, args) =>
            {
                if (Main.Client != null)
                {
                    var messages = new List<NetIncomingMessage>();
                    var msgsRead = Main.Client.ReadMessages(messages);
                    if (msgsRead <= 0) return;
                    var count = messages.Count;
                    for (var i = 0; i < count; i++)
                    {
                        CrossReference.EntryPoint.ProcessMessages(messages[i], true);
                    }
                }
            };
        }
    }

    internal static class CrossReference
    {
        public static Main EntryPoint;
    }

    internal partial class Main : Script
    {
        #region garbage
        public static PlayerSettings PlayerSettings;

        public static Size screen;

        public static readonly ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9;
        public static readonly string experimental = "exp";

        public static bool BlockControls;
        public static bool HTTPFileServer;

        public static bool IsSpectating;
        private static Vector3 _preSpectatorPos;

        internal static Streamer.Streamer NetEntityHandler;
        internal static CameraManager CameraManager;

        private readonly MenuPool _menuPool;

        public static SizeF res;


        private string _clientIp;
        public static IChat Chat;
        private static ClassicChat _backupChat;

        public static NetClient Client;
        private static NetPeerConfiguration _config;
        public static ParseableVersion CurrentVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

        internal static SynchronizationMode GlobalSyncMode;
        public static bool LerpRotaion = true;
        public static bool VehicleLagCompensation = true;
        public static bool OnFootLagCompensation = true;

        public static bool OnShootingLagCompensation = true;

        public static bool _wasTyping;

        public static bool RemoveGameEntities = true;
        public static bool ChatVisible = true;
        public static bool CanOpenChatbox = true;
        public static bool DisplayWastedMessage = true;
        public static bool ScriptChatVisible = true;
        public static bool UIVisible = true;
        public static Color UIColor = Color.White;

        public static StringCache StringCache;

        public static int LocalTeam = -1;
        public static int LocalDimension = 0;
        public int SpectatingEntity;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private string _QCpassword;

        private SyncEventWatcher Watcher;
        internal static UnoccupiedVehicleSync VehicleSyncManager;
        internal WeaponManager WeaponInventoryManager;

        private Vector3 _vinewoodSign = new Vector3(827.74f, 1295.68f, 364.34f);

        // STATS
        public static int _bytesSent = 0;
        public static int _bytesReceived = 0;

        public static int _messagesSent = 0;
        public static int _messagesReceived = 0;

        public static List<int> _averagePacketSize = new List<int>();

        private TabTextItem _statsItem;

        private static bool EnableDevTool;
        internal static bool EnableMediaStream;
        internal static bool SaveDebugToFile = false;

        public static bool ToggleNametagDraw = false;
        public static bool TogglePosUpdate = false;
        public static bool SlowDownClientForDebug = false;
        internal static bool _playerGodMode;


        public static RelationshipGroup RelGroup;
        public static RelationshipGroup FriendRelGroup;
        public static bool HasFinishedDownloading;
        public static string SocialClubName;

        #region Debug stuff
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
        #endregion

        public static bool JustJoinedServer { get; set; }


        private Process _serverProcess;

        private int _currentServerPort;
        private string _currentServerIp;

        internal static Dictionary<string, SyncPed> Npcs;
        internal static float Latency;
        private int Port = 4499;

        private GameSettings.Settings GameSettings;

        private string CustomAnimation;
        private int AnimationFlag;

        public static Camera MainMenuCamera;

        public static string GTANInstallDir = ((string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "GTANetworkInstallDir", null));

        private static int _debugStep;
        private bool _lastSpectating;
        private int _currentSpectatingPlayerIndex = 100000;
        internal SyncPed CurrentSpectatingPlayer;
        private Dictionary<int, int> _debugSettings = new Dictionary<int, int>();
        private bool _minimapSet;

        private int _lastBytesSent;
        private int _lastBytesReceived;
        private int _lastCheck;

        internal static int _bytesSentPerSecond;
        internal static int _bytesReceivedPerSecond;

        internal static Warning _mainWarning;
        internal static string _threadsafeSubtitle;
        private long _lastEntityRemoval;

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;

        public static List<int> EntityCleanup;
        public static List<int> BlipCleanup;
        public static Dictionary<int, MarkerProperties> _localMarkers = new Dictionary<int, MarkerProperties>();

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();

        public static string Weather { get; set; }
        public static TimeSpan? Time { get; set; }

        public static Stopwatch oTsw;

        private bool _init = false;

        public const NetDeliveryMethod SYNC_MESSAGE_TYPE = NetDeliveryMethod.UnreliableSequenced; // unreliable_sequenced

        #endregion


        public Main()
        {
            res = UIMenu.GetScreenResolutionMantainRatio();
            screen = GTA.UI.Screen.Resolution;

            LogManager.RuntimeLog("\r\n>> [" + DateTime.Now + "] GTA Network Initialization.");

            World.DestroyAllCameras();

            CrossReference.EntryPoint = this;

            GameSettings = Misc.GameSettings.LoadGameSettings();
            PlayerSettings = Util.Util.ReadSettings(GTANInstallDir + "\\settings.xml");

            CefUtil.DISABLE_CEF = PlayerSettings.DisableCEF;
            DebugInfo.ShowFps = PlayerSettings.ShowFPS;
            EnableMediaStream = PlayerSettings.MediaStream;
            EnableDevTool = PlayerSettings.CEFDevtool;

            _threadJumping = new Queue<Action>();

            NetEntityHandler = new Streamer.Streamer();
            CameraManager = new CameraManager();

            Watcher = new SyncEventWatcher(this);
            VehicleSyncManager = new UnoccupiedVehicleSync();
            WeaponInventoryManager = new WeaponManager();

            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            EntityCleanup = new List<int>();
            BlipCleanup = new List<int>();

            _emptyVehicleMods = new Dictionary<int, int>();
            for (var i = 0; i < 50; i++) {
                _emptyVehicleMods.Add(i, 0);
            }

            Chat = new ClassicChat();
            Chat.OnComplete += ChatOnComplete;

            _backupChat = (ClassicChat) Chat;

            LogManager.RuntimeLog("Attaching OnTick loop.");

            Tick += OnTick;
            
            KeyDown += OnKeyDown;

            KeyUp += (sender, args) => {
                if (args.KeyCode == Keys.Escape && _wasTyping) {
                    _wasTyping = false;
                }
            };

            _config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK") { Port = 8888, ConnectionTimeout = 30f };
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            LogManager.RuntimeLog("Building menu.");
            _menuPool = new MenuPool();
            BuildMainMenu();

            Function.Call(Hash._ENABLE_MP_DLC_MAPS, true); // _ENABLE_MP_DLC_MAPS
            Function.Call(Hash._LOAD_MP_DLC_MAPS); // _LOAD_MP_DLC_MAPS / _USE_FREEMODE_MAP_BEHAVIOR

            MainMenuCamera = World.CreateCamera(new Vector3(743.76f, 1070.7f, 350.24f), new Vector3(), GameplayCamera.FieldOfView);
            MainMenuCamera.PointAt(new Vector3(707.86f, 1228.09f, 333.66f));

            RelGroup = World.AddRelationshipGroup("SYNCPED");
            FriendRelGroup = World.AddRelationshipGroup("SYNCPED_TEAMMATES");

            RelGroup.SetRelationshipBetweenGroups(Game.Player.Character.RelationshipGroup, Relationship.Pedestrians, true);
            FriendRelGroup.SetRelationshipBetweenGroups(Game.Player.Character.RelationshipGroup, Relationship.Companion, true);

            SocialClubName = Game.Player.Name;

            LogManager.RuntimeLog("Getting welcome message.");
            GetWelcomeMessage();

            var t = new Thread(UpdateSocialClubAvatar) {IsBackground = true};
            t.Start();

            //Function.Call(Hash.SHUTDOWN_LOADING_SCREEN);

            Audio.SetAudioFlag(AudioFlag.LoadMPData, true);
            Audio.SetAudioFlag(AudioFlag.DisableBarks, true);
            Audio.SetAudioFlag(AudioFlag.DisableFlightMusic, true);
            Audio.SetAudioFlag(AudioFlag.PoliceScannerDisabled, true);
            Audio.SetAudioFlag(AudioFlag.OnlyAllowScriptTriggerPoliceScanner, true);
            Function.Call((Hash)0x552369F549563AD5, false); //_FORCE_AMBIENT_SIREN

            GlobalVariable.Get(2576573).Write(1); //Enable MP cars?

            LogManager.RuntimeLog("Reading whitelists.");
            ThreadPool.QueueUserWorkItem(delegate
            {
                NativeWhitelist.Init();
                SoundWhitelist.Init();
            });


            if (!PlayerSettings.DisableCEF)
            {
                LogManager.RuntimeLog("Initializing CEF.");
                CEFManager.InitializeCef();
            }

            LogManager.RuntimeLog("Rebuilding Server Browser.");
            RebuildServerBrowser();

            LogManager.RuntimeLog("Checking game files integrity.");
            IntegrityCheck();
        }

        private void Init()
        {
            if (_init) return;
            var player = Game.Player.Character;
            if (player == null || player.Handle == 0 || Game.IsLoading) return;

            LogManager.RuntimeLog("Post-Loading Initialization.");

            GTA.UI.Screen.FadeOut(1);
            ResetPlayer();
            MainMenu.RefreshIndex();
            _init = true;
            MainMenu.Visible = true;
            World.RenderingCamera = MainMenuCamera;

            DisableSlowMo();
            UnlockObjects();

            GameScript.DisableAll(PlayerSettings.DisableRockstarEditor);
            GTA.UI.Screen.FadeIn(1000);
            LogManager.RuntimeLog("Post-Loading Initialized.");
        }

        public static bool IsConnected()
        {
            return Client != null && Client.ConnectionStatus != NetConnectionStatus.Disconnected && Client.ConnectionStatus != NetConnectionStatus.None;
        }

        private void OnTick(object sender, EventArgs e)
        {
            Init();
            PauseMenu();

            //TODO: CAN BE BUGGY
            //FrameworkData.PlayerChar.Ex().MaxHealth = 200;

            if (!IsConnected()) return;
            //if (!IsOnServer()) { return; }

            try
            {
                Watcher.Tick();
                //VehicleSyncManager.Pulse();
                WeaponInventoryManager.Update();

            }
            catch (Exception ex) // Catch any other exception. (can prevent crash)
            {
                LogManager.LogException(ex, "MAIN OnTick: STEP : " + DEBUG_STEP);
            }

            //DEBUG_STEP = 7;

            //Spectate(res);

            //if (NetEntityHandler.EntityToStreamedItem(PlayerChar.Handle) is RemotePlayer playerObj)
            //{
            //    Game.Player.IsInvincible = playerObj.IsInvincible;
            //}

            //var playerChar = FrameworkData.PlayerChar.Ex();
            //if (!string.IsNullOrWhiteSpace(CustomAnimation))
            //{
            //    var sp = CustomAnimation.Split();
            //    if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, playerChar, sp[0], sp[1], 3))
            //    {
            //        playerChar.Task.ClearSecondary();
            //        Function.Call(Hash.TASK_PLAY_ANIM, playerChar, Util.Util.LoadDict(sp[0]), sp[1], 8f, 10f, -1, AnimationFlag, -8f, 1, 1, 1);
            //    }
            //}

            //DEBUG_STEP = 16;

            StringCache?.Pulse();

            //DEBUG_STEP = 17;

            //double aver = 0;
            //lock (_averagePacketSize)
            //{
            //    aver = _averagePacketSize.Count > 0 ? _averagePacketSize.Average() : 0;
            //}

            //_statsItem.Text = string.Format(
            //        "~h~Bytes Sent~h~: {0}~n~~h~Bytes Received~h~: {1}~n~~h~Bytes Sent / Second~h~: {5}~n~~h~Bytes Received / Second~h~: {6}~n~~h~Average Packet Size~h~: {4}~n~~n~~h~Messages Sent~h~: {2}~n~~h~Messages Received~h~: {3}",
            //        aver, _bytesSentPerSecond, _bytesReceivedPerSecond);

            //DEBUG_STEP = 21;

            lock (_threadJumping)
            {
                if (_threadJumping.Any())
                {
                    var action = _threadJumping.Dequeue();
                    action?.Invoke();
                }
            }
            //DEBUG_STEP = 41;
            //if (DebugInfo.StreamerDebug) oTsw.Stop();
        }



        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Chat.OnKeyDown(e.KeyCode);
            Passenger();

            if (e.KeyCode == Keys.Escape && Client != null && Client.ConnectionStatus == NetConnectionStatus.Disconnected)
            {
                Client.Disconnect("Connection canceled.");
            }

            if (e.KeyCode == Keys.F10 && !Chat.IsFocused)
            {
                MainMenu.Visible = !MainMenu.Visible;

                if (!IsOnServer())
                {
                    World.RenderingCamera = MainMenu.Visible ? MainMenuCamera : null;
                }
                else if (MainMenu.Visible)
                {
                    RebuildPlayersList();
                }

                MainMenu.RefreshIndex();
            }

            if (e.KeyCode == Keys.F7 && IsOnServer())
            {
                ChatVisible = !ChatVisible;
                UIVisible = !UIVisible;
                Function.Call(Hash.DISPLAY_RADAR, UIVisible);
                Function.Call(Hash.DISPLAY_HUD, UIVisible);
            }

            if (e.KeyCode == PlayerSettings.ScreenshotKey && IsOnServer())
            {
                Screenshot.TakeScreenshot();
            }

            if (e.KeyCode != Keys.T || !IsOnServer() || !UIVisible || !ChatVisible || !ScriptChatVisible || !CanOpenChatbox) return;

            if (!_oldChat)
            {
                Chat.IsFocused = true;
                _wasTyping = true;
            }
            else
            {
                var message = Game.GetUserInput(255);
                if (string.IsNullOrEmpty(message)) return;

                var obj = new ChatData { Message = message, };
                var data = SerializeBinary(obj);

                var msg = Client?.CreateMessage();
                msg?.Write((byte)PacketType.ChatData);
                msg?.Write(data.Length);
                msg?.Write(data);
                Client?.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
            }
        }

    }
}
