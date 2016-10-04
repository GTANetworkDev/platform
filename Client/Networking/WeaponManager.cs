using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using WeaponHash = GTANetworkShared.WeaponHash;

namespace GTANetwork.Networking
{
    public class WeaponManager
    {
        private List<WeaponHash> _playerInventory = new List<WeaponHash>
        {
            WeaponHash.Unarmed,
        };

        public void Clear()
        {
            _playerInventory.Clear();
            _playerInventory.Add(WeaponHash.Unarmed);
        }

        public void Update()
        {
            var weapons = Enum.GetValues(typeof (WeaponHash)).Cast<WeaponHash>();

            foreach (var hash in weapons)
            {
                if (!_playerInventory.Contains(hash))
                {
                    Game.Player.Character.Weapons.Remove((GTA.WeaponHash)(int)hash);
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