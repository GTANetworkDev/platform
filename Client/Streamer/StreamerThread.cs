using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTANetwork.Util;
using GTANetwork.Sync;
using Vector3 = GTA.Math.Vector3;
using System.Diagnostics;

namespace GTANetwork.Streamer
{
    internal class StreamerThread : Script
    {
        public static ParallelQuery<SyncPed> SyncPeds;
        public static SyncPed[] StreamedInPlayers;

        private List<IStreamedItem> _itemsToStreamIn;
        private List<IStreamedItem> _itemsToStreamOut;

        public static Stopwatch sw;

        public StreamerThread()
        {
            _itemsToStreamIn = new List<IStreamedItem>();
            _itemsToStreamOut = new List<IStreamedItem>();
            StreamedInPlayers = new SyncPed[MAX_PLAYERS+1];

            Tick += StreamerTick;

            System.Threading.Thread calcucationThread = new System.Threading.Thread(StreamerCalculationsThread) { IsBackground = true };
            calcucationThread.Start();
        }

        private static Vector3 _playerPosition;

        public const int MAX_PLAYERS = 250; //Max engine ped value: 256, on 236 it starts to cause issues
        public const int MAX_OBJECTS = 2000; //Max engine value: 1999
        public const int MAX_VEHICLES = 60; //Max engine value: 64 +/ 1
        public const int MAX_PICKUPS = 50; //NEEDS A TEST
        public const int MAX_BLIPS = 50; //Max engine value: 1298
        public static int MAX_PEDS; //Share the Ped limit, prioritize the players
        public const int MAX_LABELS = MAX_PLAYERS; //NEEDS A TEST
        public const int MAX_MARKERS = 120; //Max engine value: 128
        public const int MAX_PARTICLES = 50;

        public const float GlobalRange = 1500f;
        public const float LongRange = 1000f;
        public const float CloseRange = 500f;


        private void StreamerCalculationsThread()
        {
            while (true)
            {
                if (!Main.IsOnServer() || !Main.HasFinishedDownloading) goto endTick;
                var position = _playerPosition.ToLVector();

                var rawMap = new List<IStreamedItem>(Main.NetEntityHandler.ClientMap.Values).Where(item => !(item is RemotePlayer) || ((RemotePlayer) item).LocalHandle != -2);

                SyncPeds = new List<IStreamedItem>(rawMap).AsParallel().OfType<SyncPed>().OrderBy(item => item.Position.ToLVector().DistanceToSquared(position));
                var streamedInPlayers = SyncPeds.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position.ToLVector(), CloseRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInPlayers.Where(item => !item.StreamedIn).Take(MAX_PLAYERS));
                lock (StreamedInPlayers) { StreamedInPlayers = streamedInPlayers.Take(MAX_PLAYERS).ToArray(); }

                var streamedOutPlayers = SyncPeds.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0 || !IsInRange(position, item.Position.ToLVector(), CloseRange)) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInPlayers.Skip(MAX_PLAYERS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutPlayers);
                }


                var entityMap = new List<IStreamedItem>(rawMap).Where(item => item.Position != null);

                var Peds = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemotePed>().OrderBy(item => item.Position.DistanceToSquared(position));
                MAX_PEDS = MAX_PLAYERS - streamedInPlayers.Take(MAX_PLAYERS).Count();
                var streamedInPeds = Peds.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, CloseRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInPeds.Take(MAX_PEDS).Where(item => !item.StreamedIn));

                var streamedOutPeds = Peds.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0 || !IsInRange(position, item.Position, CloseRange)) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInPeds.Skip(MAX_PEDS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutPeds);
                }


                var Vehicles = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteVehicle>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInVehicles = Vehicles.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, LongRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInVehicles.Take(MAX_VEHICLES).Where(item => !item.StreamedIn));

                var streamedOutVehicles = Vehicles.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0) || !IsInRange(position, item.Position, LongRange) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInVehicles.Skip(MAX_VEHICLES).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutVehicles);
                }


                var Objects = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteProp>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInObjects = Objects.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, GlobalRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInObjects.Take(MAX_OBJECTS).Where(item => !item.StreamedIn));

                var streamedOutObjects = Objects.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0 || !IsInRange(position, item.Position, GlobalRange)) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInObjects.Skip(MAX_OBJECTS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutObjects);
                }


                var Labels = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteTextLabel>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInLabels = Labels.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, CloseRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInLabels.Take(MAX_LABELS).Where(item => !item.StreamedIn));

                var streamedOutLabels = Labels.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0 || !IsInRange(position, item.Position, CloseRange)) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInLabels.Skip(MAX_LABELS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutLabels);
                }


                var Markers = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteBlip>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInMarkers = Markers.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, GlobalRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInMarkers.Take(MAX_MARKERS).Where(item => !item.StreamedIn));

                var streamedOutMarkers = Markers.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0) || !IsInRange(position, item.Position, GlobalRange) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInMarkers.Skip(MAX_MARKERS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutMarkers);
                }

                var Particles = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteParticle>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInParticles = Particles.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, CloseRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInParticles.Take(MAX_PARTICLES).Where(item => !item.StreamedIn));

                var streamedOutParticles = Particles.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0) || !IsInRange(position, item.Position, CloseRange) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInParticles.Skip(MAX_PARTICLES).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutParticles);
                }


                var Pickups = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemotePickup>().OrderBy(item => item.Position.DistanceToSquared(position));
                var streamedInPickups = Pickups.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, LongRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInPickups.Take(MAX_PICKUPS).Where(item => !item.StreamedIn));

                var streamedOutPickups = Pickups.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0) || !IsInRange(position, item.Position, LongRange) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInPickups.Skip(MAX_PICKUPS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutPickups);
                }


                var Blips = new List<IStreamedItem>(entityMap).AsParallel().OfType<RemoteBlip>().OrderBy(item => item.Position.DistanceToSquared2D(position));
                var streamedInBlips = Blips.Where(item => (item.Dimension == Main.LocalDimension || item.Dimension == 0) && IsInRange(position, item.Position, GlobalRange));
                lock (_itemsToStreamIn) _itemsToStreamIn.AddRange(streamedInBlips.Take(MAX_BLIPS).Where(item => !item.StreamedIn));

                var streamedOutBlips = Blips.Where(item => (item.Dimension != Main.LocalDimension && item.Dimension != 0) || !IsInRange(position, item.Position, GlobalRange) && item.StreamedIn);
                lock (_itemsToStreamOut)
                {
                    _itemsToStreamOut.AddRange(streamedInBlips.Skip(MAX_BLIPS).Where(item => item.StreamedIn));
                    _itemsToStreamOut.AddRange(streamedOutBlips);
                }


                endTick:
                System.Threading.Thread.Sleep(1000);
            }
        }

        private void StreamerTick(object sender, EventArgs e)
        {
            _playerPosition = Game.Player.Character.Position;
            if (Util.Util.ModelRequest) return;
            sw = new Stopwatch();

            if (DebugInfo.StreamerDebug) sw.Start();

            lock (_itemsToStreamOut)
            {
                for (var index = 0; index < _itemsToStreamOut.Count; index++)
                {
                    Main.NetEntityHandler.StreamOut(_itemsToStreamOut[index]);
                }
                _itemsToStreamOut.Clear();
            }

            lock (_itemsToStreamIn)
            {
                for (var index = 0; index < _itemsToStreamIn.Count; index++)
                {
                    Main.NetEntityHandler.StreamIn(_itemsToStreamIn[index]);
                }
                _itemsToStreamIn.Clear();
            }

            if (DebugInfo.StreamerDebug) sw.Stop();
        }

        private static bool IsInRange(GTANetworkShared.Vector3 center, GTANetworkShared.Vector3 dest, float range)
        {
            return center.Subtract(dest).Length() <= range;
        }
    }

}