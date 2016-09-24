using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using GTANetworkServer.Constant;
using GTANetworkShared;
using Lidgren.Network;

namespace GTANetworkServer
{
    internal static class StressTest
    {
        public static int PlayersToSim = 500;
        public static bool HasPlayers;
        public static List<Client> Players = new List<Client>();

        public static void Init()
        {
            Thread t = new Thread(Pulse);
            t.IsBackground = true;
            t.Start();
        }

        private static Random _randObj = new Random();
        public static string RandomString(int len)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < len; i++)
            {
                sb.Append((char) _randObj.Next(65, 123));
            }

            return sb.ToString();
        }

        public static void CreatePlayer()
        {
            var data = new Client(null);
            data.Name = RandomString(10);
            data.Fake = true;
            data.CommitConnection();

            Players.Add(data);
            Program.ServerInstance.Clients.Add(data);

            var delta = new Delta_PlayerProperties();
            delta.Name = data.Name;
            Program.ServerInstance.UpdateEntityInfo(data.CharacterHandle.Value, EntityType.Player, delta, data);

            Program.Output("Adding player " + data.Name);
        }

        public static void UpdatePlayer(Client player)
        {
            var data = new PedData();
            
            if (player.Position == null) player.Position = Vector3.RandomXY() * 3000f * (float)_randObj.NextDouble();
            data.Position = player.Position;
            player.LastUpdate = DateTime.Now;

            data.NetHandle = player.CharacterHandle.Value;
            data.PedArmor = 0;
            data.Flag = 0;
            data.PedModelHash = (int) PedHash.Michael;
            data.PlayerHealth = 100;
            data.Quaternion = new Vector3();
            data.Speed = 0;
            data.Velocity = new Vector3();
            data.WeaponHash = (int)WeaponHash.Unarmed;
            data.Latency = 0.1f;
            
            Program.ServerInstance.ResendPacket(data, player, true);

            if (Environment.TickCount - player.GameVersion > 1500)
            {
                Program.ServerInstance.ResendPacket(data, player, false);
                player.GameVersion = Environment.TickCount;
            }
        }

        private static int _simId = 100;

        public static void Pulse()
        {
            while (true)
            {
                if (HasPlayers && Players.Count < PlayersToSim)
                {
                    CreatePlayer();
                }

                foreach (var client in Players)
                {
                    if (DateTime.Now.Subtract(client.LastUpdate).TotalMilliseconds > 100)
                    {
                        UpdatePlayer(client);
                    }
                }

                Thread.Yield();
            }
        }
    }
}