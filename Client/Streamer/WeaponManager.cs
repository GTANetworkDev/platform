using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTANetwork.Util;
using WeaponHash = GTANetworkShared.WeaponHash;

namespace GTANetwork.Streamer
{
    public class WeaponManager : Script
    {

        public WeaponManager()
        {
            Tick += OnTick;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            if (Main.IsConnected())
            {
                Update();
            }
        }

        private static List<WeaponHash> _playerInventory = new List<WeaponHash>
        {
            WeaponHash.Unarmed
        };

        public void Clear()
        {
            _playerInventory.Clear();
            _playerInventory.Add(WeaponHash.Unarmed);
        }

        private static DateTime LastDateTime = DateTime.Now;
        internal static void Update()
        {
            if (DateTime.Now.Subtract(LastDateTime).TotalMilliseconds >= 500)
            {
                LastDateTime = DateTime.Now;
                var weapons = Enum.GetValues(typeof(WeaponHash)).Cast<WeaponHash>();
                foreach (var hash in weapons)
                {
                    if (!_playerInventory.Contains(hash) && hash != WeaponHash.Unarmed)
                    {
                        Game.Player.Character.Weapons.Remove((GTA.WeaponHash)(int)hash);
                    }
                }
            }

        }

        public void Allow(WeaponHash hash)
        {
            if (!_playerInventory.Contains(hash)) _playerInventory.Add(hash);
        }

        public void Deny(WeaponHash hash)
        {
            _playerInventory.Remove(hash);
        }
    }
}