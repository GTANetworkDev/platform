using System;
using System.Collections.Generic;
using System.Linq;
using GTA;

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
                    Game.Player.Character.Weapons.Remove(hash);
                }
            }

            if (!_playerInventory.Contains((WeaponHash)4222310262)) // parachute
            {
                Game.Player.Character.Weapons.Remove((WeaponHash)4222310262);
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