using System;
using GTA;
using GTANetwork.Javascript;
using GTANetwork.Streamer;
using GTANetworkShared;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace GTANetwork.Util
{
    public class StickyBombTracker : Script
    {
        public StickyBombTracker()
        {
            base.Tick += OnTick;
        }

        private bool _hasPlacedStickies;

        private void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;
            if (player.IsShooting && player.Weapons.Current.Hash == (WeaponHash.StickyBomb))
            {
                _hasPlacedStickies = true;
            }

            if (Game.Player.IsDead)
            {
                _hasPlacedStickies = false;
            }

            if (Game.IsControlJustPressed(0, Control.Detonate) && _hasPlacedStickies)
            {
                SyncEventWatcher.SendSyncEvent(SyncEventType.StickyBombDetonation, Main.NetEntityHandler.EntityToNet(player.Handle));
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerDetonateStickies());
                _hasPlacedStickies = false;
            }
        }
    }
}