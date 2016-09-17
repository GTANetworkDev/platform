using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GTANetworkServer
{
    internal class Streamer
    {
        public static bool Stop;
        public static void MainThread()
        {
            while (!Stop)
            {
                foreach (var client in Program.ServerInstance.PublicAPI.getAllPlayers())
                {
                    try
                    {
                        client.Streamer.Pulse();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                Thread.Sleep(100);
            }
        }

        public Streamer(Client f)
        {
            _parent = f;
        }

        private readonly Client _parent;
        private List<Client> _ordered = new List<Client>();
        private long _lastUpdate;

        public IEnumerable<Client> GetNearClients()
        {
            return new List<Client>(_ordered.Take(40));
        }

        public IEnumerable<Client> GetFarClients()
        {
            return new List<Client>(_ordered.Skip(40));
        }

        public void Pulse()
        {
            if (Program.GetTicks() - _lastUpdate > 1000)
            {
                _lastUpdate = Program.GetTicks();

                lock (_ordered)
                {
                    var list = Program.ServerInstance.PublicAPI.getAllPlayers().Except(new [] { _parent });
                    _ordered = new List<Client>(list.OrderBy(c => _parent.Position.DistanceToSquared(c.Position)));
                }
            }
        }
    }
}