using System;
using System.Collections.Generic;
using static System.BitConverter;

namespace GTANetworkShared
{
    public static class PacketOptimization
    {
        #region Write Operations

        public static byte[] WritePureSync(PedData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write player's nethandle.
            if (data.NetHandle.HasValue)
            {
                byteArray.AddRange(GetBytes(data.NetHandle.Value));
            }
            else
            {
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
            }

            // Write the flag
            byteArray.AddRange(GetBytes(data.Flag.Value));

            // Write player's position, rotation, and velocity
            byteArray.AddRange(GetBytes(data.Position.X));
            byteArray.AddRange(GetBytes(data.Position.Y));
            byteArray.AddRange(GetBytes(data.Position.Z));

            // Only send roll & pitch if we're parachuting.
            if (CheckBit(data.Flag.Value, PedDataFlags.ParachuteOpen))
            {
                byteArray.AddRange(GetBytes(data.Quaternion.X));
                byteArray.AddRange(GetBytes(data.Quaternion.Y));
            }

            byteArray.AddRange(GetBytes(data.Quaternion.Z));

            byteArray.AddRange(GetBytes(data.Velocity.X));
            byteArray.AddRange(GetBytes(data.Velocity.Y));
            byteArray.AddRange(GetBytes(data.Velocity.Z));
            
            // Write player health, armor and walking speed
            byteArray.Add(data.PlayerHealth.Value);
            byteArray.Add(data.PedArmor.Value);
            byteArray.Add(data.Speed.Value);


            // Write current weapon hash.
            byteArray.AddRange(GetBytes(data.WeaponHash.Value));

            // Are we shooting?
            if (CheckBit(data.Flag.Value, PedDataFlags.Aiming) ||
                CheckBit(data.Flag.Value, PedDataFlags.Shooting) ||
                CheckBit(data.Flag.Value, PedDataFlags.HasAimData))
            {
                // Aim coordinates
                byteArray.AddRange(GetBytes(data.AimCoords.X));
                byteArray.AddRange(GetBytes(data.AimCoords.Y));
                byteArray.AddRange(GetBytes(data.AimCoords.Z));
            }

            // Are we entering a car?
            if (CheckBit(data.Flag.Value, PedDataFlags.EnteringVehicle))
            {
                // Add the car we are trying to enter
                byteArray.AddRange(GetBytes(data.VehicleTryingToEnter.Value));

                // Add the seat we are trying to enter
                byteArray.Add((byte)data.SeatTryingToEnter.Value);
            }
            
            return byteArray.ToArray();
        }

        public static byte[] WriteLightSync(PedData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write player's nethandle.
            if (data.NetHandle.HasValue)
            {
                byteArray.AddRange(GetBytes(data.NetHandle.Value));
            }
            else
            {
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
            }

            // Write player model
            byteArray.AddRange(GetBytes(data.PedModelHash.Value));
            
            // Write player's latency
            if (data.Latency.HasValue)
            {
                var latency = data.Latency.Value*1000;
                byteArray.AddRange(GetBytes((short) latency));
            }

            return byteArray.ToArray();
        }

        public static byte[] WritePureSync(VehicleData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write player's nethandle.
            if (data.NetHandle.HasValue)
            {
                byteArray.AddRange(GetBytes(data.NetHandle.Value));
            }
            else
            {
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
            }

            // Write player health and armor
            byteArray.Add(data.PlayerHealth.Value);
            byteArray.Add(data.PedArmor.Value);

            // Write the flag
            byteArray.AddRange(GetBytes(data.Flag.Value));

            if (CheckBit(data.Flag.Value, VehicleDataFlags.Aiming) ||
                CheckBit(data.Flag.Value, VehicleDataFlags.HasAimData) ||
                CheckBit(data.Flag.Value, VehicleDataFlags.Shooting))
            {
                // Write the gun model
                byteArray.AddRange(GetBytes(data.WeaponHash.Value));

                // Write the aiming point
                byteArray.AddRange(GetBytes(data.AimCoords.X));
                byteArray.AddRange(GetBytes(data.AimCoords.Y));
                byteArray.AddRange(GetBytes(data.AimCoords.Z));
            }
            
            // Are we the driver?
            if (CheckBit(data.Flag.Value, VehicleDataFlags.Driver))
            {
                // Write vehicle position, rotation and velocity
                byteArray.AddRange(GetBytes(data.Position.X));
                byteArray.AddRange(GetBytes(data.Position.Y));
                byteArray.AddRange(GetBytes(data.Position.Z));

                byteArray.AddRange(GetBytes(data.Quaternion.X));
                byteArray.AddRange(GetBytes(data.Quaternion.Y));
                byteArray.AddRange(GetBytes(data.Quaternion.Z));


                byteArray.AddRange(GetBytes(data.Velocity.X));
                byteArray.AddRange(GetBytes(data.Velocity.Y));
                byteArray.AddRange(GetBytes(data.Velocity.Z));

                // Write vehicle health
                byteArray.AddRange(GetBytes((short) ((int) data.VehicleHealth.Value)));

                // Write engine stuff
                byte rpm = (byte) (data.RPM.Value*byte.MaxValue);

                float angle = Extensions.Clamp(data.Steering.Value, -60f, 60f);
                angle += 60f;
                byte angleCrammed = (byte) ((angle/120f)*byte.MaxValue);

                byteArray.Add(rpm);
                byteArray.Add(angleCrammed);
            }

            return byteArray.ToArray();
        }

        public static byte[] WriteLightSync(VehicleData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write player's nethandle.
            if (data.NetHandle.HasValue)
            {
                byteArray.AddRange(GetBytes(data.NetHandle.Value));
            }
            else
            {
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
                byteArray.Add(0x00);
            }

            // Write player model
            byteArray.AddRange(GetBytes(data.PedModelHash.Value));

            // Write his vehicle handle
            byteArray.AddRange(GetBytes(data.VehicleHandle.Value));

            // Write his seat
            byteArray.Add((byte) data.VehicleSeat.Value);

            // Write the gun model
            byteArray.AddRange(GetBytes(data.WeaponHash.Value));

            // Write vehicle damage model
            if (data.DamageModel != null)
            {
                byteArray.Add(0x01);
                byteArray.Add(data.DamageModel.BrokenDoors); // Write doors
                byteArray.Add(data.DamageModel.BrokenWindows); // Write windows
                byteArray.AddRange(GetBytes(data.DamageModel.BrokenLights)); // Lights
            }
            else
            {
                byteArray.Add(0x00);
            }

            // If he has a trailer attached, write it's position. (Maybe we can use his pos & rot to calculate it serverside?)
            if (data.Trailer != null)
            {
                byteArray.Add(0x01);
                byteArray.AddRange(GetBytes(data.Trailer.X));
                byteArray.AddRange(GetBytes(data.Trailer.Y));
                byteArray.AddRange(GetBytes(data.Trailer.Z));
            }
            else
            {
                byteArray.Add(0x00);
            }

            // Write player's latency
            if (data.Latency.HasValue)
            {
                var latency = data.Latency.Value * 1000;
                byteArray.AddRange(GetBytes((short)latency));
            }

            return byteArray.ToArray();
        }

        public static byte[] WriteBasicSync(int netHandle, Vector3 position)
        {
            List<byte> byteArray = new List<byte>();

            // write the player nethandle
            byteArray.AddRange(GetBytes(netHandle));

            // Write his position
            byteArray.AddRange(GetBytes(position.X));
            byteArray.AddRange(GetBytes(position.Y));
            byteArray.AddRange(GetBytes(position.Z));

            return byteArray.ToArray();
        }

        public static byte[] WriteBulletSync(int netHandle, bool shooting, Vector3 aimCoords)
        {
            List<byte> byteArray = new List<byte>();

            // write the player nethandle
            byteArray.AddRange(GetBytes(netHandle));

            // is he shooting anymore?
            byteArray.Add(shooting ? (byte)0x01 : (byte)0x00);

            if (shooting)
            {
                // Write his aiming point
                byteArray.AddRange(GetBytes(aimCoords.X));
                byteArray.AddRange(GetBytes(aimCoords.Y));
                byteArray.AddRange(GetBytes(aimCoords.Z));
            }

            return byteArray.ToArray();
        }

        public static byte[] WriteUnOccupiedVehicleSync(VehicleData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write vehicle's nethandle.
            byteArray.AddRange(GetBytes(data.VehicleHandle.Value));
            
            // Write vehicle position, rotation and velocity
            byteArray.AddRange(GetBytes(data.Position.X));
            byteArray.AddRange(GetBytes(data.Position.Y));
            byteArray.AddRange(GetBytes(data.Position.Z));

            byteArray.AddRange(GetBytes(data.Quaternion.X));
            byteArray.AddRange(GetBytes(data.Quaternion.Y));
            byteArray.AddRange(GetBytes(data.Quaternion.Z));

            byteArray.AddRange(GetBytes(data.Velocity.X));
            byteArray.AddRange(GetBytes(data.Velocity.Y));
            byteArray.AddRange(GetBytes(data.Velocity.Z));

            // Write vehicle health
            byteArray.AddRange(GetBytes((short)((int)data.VehicleHealth.Value)));

            // Set if whether its dead
            if (CheckBit(data.Flag.Value, VehicleDataFlags.VehicleDead))
            {
                byteArray.Add(0x01);
            }
            else
            {
                byteArray.Add(0x00);
            }

            // Write the tyre state, using the playerhealth in VehicleData
            byteArray.Add(data.PlayerHealth.Value);

            // Write vehicle damage model
            byteArray.Add(data.DamageModel.BrokenDoors); // Write doors
            byteArray.Add(data.DamageModel.BrokenWindows); // Write windows

            return byteArray.ToArray();
        }

        public static byte[] WriteBasicUnOccupiedVehicleSync(VehicleData data)
        {
            List<byte> byteArray = new List<byte>();

            // Write vehicle's nethandle.
            byteArray.AddRange(GetBytes(data.VehicleHandle.Value));

            // Write vehicle position and heading
            byteArray.AddRange(GetBytes(data.Position.X));
            byteArray.AddRange(GetBytes(data.Position.Y));
            byteArray.AddRange(GetBytes(data.Position.Z));

            byteArray.AddRange(GetBytes(data.Quaternion.Z));
            
            // Write vehicle health
            byteArray.AddRange(GetBytes((short)((int)data.VehicleHealth.Value)));

            // Set if whether its dead
            if (CheckBit(data.Flag.Value, VehicleDataFlags.VehicleDead))
            {
                byteArray.Add(0x01);
            }
            else
            {
                byteArray.Add(0x00);
            }

            // Write the tyre state, using the playerhealth in VehicleData
            byteArray.Add(data.PlayerHealth.Value);

            // Write vehicle damage model
            byteArray.Add(data.DamageModel.BrokenDoors); // Write doors
            byteArray.Add(data.DamageModel.BrokenWindows); // Write windows

            return byteArray.ToArray();
        }

        #endregion

        #region Read Operations

        public static PedData ReadPurePedSync(byte[] array)
        {
            var data = new PedData();
            var r = new BitReader(array);

            // Read player nethandle
            data.NetHandle = r.ReadInt32();

            // Read the flag
            data.Flag = r.ReadInt32();

            // Read player position, rotation and velocity
            Vector3 position = new Vector3();
            Vector3 rotation = new Vector3();
            Vector3 velocity = new Vector3();

            position.X = r.ReadSingle();
            position.Y = r.ReadSingle();
            position.Z = r.ReadSingle();

            // Only read pitchand roll if he's ragdolling
            if (CheckBit(data.Flag.Value, PedDataFlags.ParachuteOpen))
            {
                rotation.X = r.ReadSingle();
                rotation.Y = r.ReadSingle();
            }

            rotation.Z = r.ReadSingle();

            velocity.X = r.ReadSingle();
            velocity.Y = r.ReadSingle();
            velocity.Z = r.ReadSingle();

            data.Position = position;
            data.Quaternion = rotation;
            data.Velocity = velocity;

            // Read health, armor and speed
            data.PlayerHealth = r.ReadByte();
            data.PedArmor = r.ReadByte();
            data.Speed = r.ReadByte();

            // read gun model
            data.WeaponHash = r.ReadInt32();

            // Is the player shooting?
            if (CheckBit(data.Flag.Value, PedDataFlags.Aiming) ||
                CheckBit(data.Flag.Value, PedDataFlags.Shooting) ||
                CheckBit(data.Flag.Value, PedDataFlags.HasAimData))
            {
                // read where is he aiming
                Vector3 aimPoint = new Vector3();

                aimPoint.X = r.ReadSingle();
                aimPoint.Y = r.ReadSingle();
                aimPoint.Z = r.ReadSingle();

                data.AimCoords = aimPoint;
            }

            if (CheckBit(data.Flag.Value, PedDataFlags.EnteringVehicle))
            {
                data.VehicleTryingToEnter = r.ReadInt32();

                data.SeatTryingToEnter = (sbyte)r.ReadByte();
            }

            return data;
        }

        public static PedData ReadLightPedSync(byte[] array)
        {
            var data = new PedData();
            var r = new BitReader(array);

            // Read player nethandle
            data.NetHandle = r.ReadInt32();
            
            // Read player model
            data.PedModelHash = r.ReadInt32();

            // If we can, read latency

            if (r.CanRead(2))
            {
                var latency = r.ReadInt16();

                data.Latency = latency/1000f;
            }
            
            return data;
        }

        public static VehicleData ReadPureVehicleSync(byte[] array)
        {
            var data = new VehicleData();
            var r = new BitReader(array);

            // Read player nethandle
            data.NetHandle = r.ReadInt32();

            // read health values
            data.PlayerHealth = r.ReadByte();
            data.PedArmor = r.ReadByte();

            // read flag
            data.Flag = r.ReadInt16();

            // If we're shooting/aiming, read gun stuff
            if (CheckBit(data.Flag.Value, VehicleDataFlags.Shooting) ||
                CheckBit(data.Flag.Value, VehicleDataFlags.HasAimData) ||
                CheckBit(data.Flag.Value, VehicleDataFlags.Aiming))
            {
                // read gun model
                data.WeaponHash = r.ReadInt32();

                // read aim coordinates
                Vector3 aimCoords = new Vector3();

                aimCoords.X = r.ReadSingle();
                aimCoords.Y = r.ReadSingle();
                aimCoords.Z = r.ReadSingle();

                data.AimCoords = aimCoords;
            }

            // Are we the driver?
            if (CheckBit(data.Flag.Value, VehicleDataFlags.Driver))
            {
                // Read position, rotation and velocity.
                Vector3 position = new Vector3();
                Vector3 rotation = new Vector3();
                Vector3 velocity = new Vector3();

                position.X = r.ReadSingle();
                position.Y = r.ReadSingle();
                position.Z = r.ReadSingle();

                rotation.X = r.ReadSingle();
                rotation.Y = r.ReadSingle();
                rotation.Z = r.ReadSingle();

                velocity.X = r.ReadSingle();
                velocity.Y = r.ReadSingle();
                velocity.Z = r.ReadSingle();

                data.Position = position;
                data.Quaternion = rotation;
                data.Velocity = velocity;

                // Read car health
                data.VehicleHealth = r.ReadInt16();

                // read RPM & steering angle
                byte rpmCompressed = r.ReadByte();
                data.RPM = rpmCompressed/(float) byte.MaxValue;

                byte angleCompressed = r.ReadByte();
                var angleDenorm = 120f*(angleCompressed/(float) byte.MaxValue);
                data.Steering = angleDenorm - 60f;
            }

            return data;
        }

        public static VehicleData ReadLightVehicleSync(byte[] array)
        {
            var data = new VehicleData();
            var r = new BitReader(array);

            // Read player nethandle
            data.NetHandle = r.ReadInt32();
            
            // read model
            data.PedModelHash = r.ReadInt32();

            // read vehicle handle
            data.VehicleHandle = r.ReadInt32();

            // read vehicle seat
            data.VehicleSeat = (sbyte)r.ReadByte();

            // read gun model.
            data.WeaponHash = r.ReadInt32();

            // Read vehicle damage model
            if (r.ReadBoolean())
            {
                data.DamageModel = new VehicleDamageModel();
                data.DamageModel.BrokenDoors = r.ReadByte();
                data.DamageModel.BrokenWindows = r.ReadByte();
                data.DamageModel.BrokenLights = r.ReadInt32();
            }

            // Does he have a traielr?
            if (r.ReadBoolean())
            {
                Vector3 trailerPos = new Vector3();

                trailerPos.X = r.ReadSingle();
                trailerPos.Y = r.ReadSingle();
                trailerPos.Z = r.ReadSingle();

                data.Trailer = trailerPos;
            }

            // Try to read latency
            if (r.CanRead(2))
            {
                var latency = r.ReadInt16();
                data.Latency = latency/1000f;
            }

            return data;
        }

        public static void ReadBasicSync(byte[] array, out int netHandle, out Vector3 position)
        {
            var r = new BitReader(array);

            // read netHandle
            netHandle = r.ReadInt32();

            // read position
            position = new Vector3();

            position.X = r.ReadSingle();
            position.Y = r.ReadSingle();
            position.Z = r.ReadSingle();
        }

        public static bool ReadBulletSync(byte[] array, out int netHandle, out Vector3 position)
        {
            var r = new BitReader(array);

            // read netHandle
            netHandle = r.ReadInt32();

            // read whether he's shooting
            bool output = r.ReadBoolean();

            position = new Vector3();

            // read aiming point
            if (output)
            {
                position.X = r.ReadSingle();
                position.Y = r.ReadSingle();
                position.Z = r.ReadSingle();
            }
            return output;
        }

        public static VehicleData ReadUnoccupiedVehicleSync(byte[] array)
        {
            var r = new BitReader(array);
            var data = new VehicleData();

            // Read vehicle's nethandle.
            data.VehicleHandle = r.ReadInt32();

            // Read position, rotation and velocity.
            Vector3 position = new Vector3();
            Vector3 rotation = new Vector3();
            Vector3 velocity = new Vector3();

            position.X = r.ReadSingle();
            position.Y = r.ReadSingle();
            position.Z = r.ReadSingle();

            rotation.X = r.ReadSingle();
            rotation.Y = r.ReadSingle();
            rotation.Z = r.ReadSingle();

            velocity.X = r.ReadSingle();
            velocity.Y = r.ReadSingle();
            velocity.Z = r.ReadSingle();

            data.Position = position;
            data.Quaternion = rotation;
            data.Velocity = velocity;

            // Read vehicle health
            data.VehicleHealth = r.ReadInt16();

            // Read is dead
            if (r.ReadBoolean())
                data.Flag = (short) VehicleDataFlags.VehicleDead;
            else
                data.Flag = 0;

            // Read tyre states
            data.PlayerHealth = r.ReadByte();

            // Read vehicle damage model
            data.DamageModel = new VehicleDamageModel();
            data.DamageModel.BrokenDoors = r.ReadByte();
            data.DamageModel.BrokenWindows = r.ReadByte();

            return data;
        }

        public static VehicleData ReadBasicUnoccupiedVehicleSync(byte[] array)
        {
            var r = new BitReader(array);
            var data = new VehicleData();

            // Read vehicle's nethandle.
            data.VehicleHandle = r.ReadInt32();

            // Read position and heading
            Vector3 position = new Vector3();
            Vector3 rotation = new Vector3();
            
            position.X = r.ReadSingle();
            position.Y = r.ReadSingle();
            position.Z = r.ReadSingle();

            rotation.Z = r.ReadSingle();
            
            data.Position = position;
            data.Quaternion = rotation;

            // Read vehicle health
            data.VehicleHealth = r.ReadInt16();

            // Read is dead
            if (r.ReadBoolean())
                data.Flag |= (short) VehicleDataFlags.VehicleDead;
            else
                data.Flag = 0;

            // Read tyre states.
            data.PlayerHealth = r.ReadByte();

            // Read vehicle damage model
            data.DamageModel = new VehicleDamageModel();
            data.DamageModel.BrokenDoors = r.ReadByte();
            data.DamageModel.BrokenWindows = r.ReadByte();

            return data;
        }

        #endregion


        public static ushort CompressSingle(float value)
        {
            return (ushort) (value*256);
        }

        public static float DecompressSingle(ushort value)
        {
            return value/256f;
        }
        
        public static bool CheckBit(int value, VehicleDataFlags flag)
        {
            return (value & (int)flag) != 0;
        }

        public static bool CheckBit(int value, PedDataFlags flag)
        {
            return (value & (int)flag) != 0;
        }

        public static bool CheckBit(int value, EntityFlag flag)
        {
            return (value & (int)flag) != 0;
        }

        public static int SetBit(int value, EntityFlag flag)
        {
            return value |= (int) flag;
        }

        public static int ResetBit(int value, EntityFlag flag)
        {
            return value &= ~(int) flag;
        }

        public static bool CheckBit(int value, int flag)
        {
            return (value & (int)flag) != 0;
        }

        public static int SetBit(int value, int flag)
        {
            return value |= (int)flag;
        }

        public static int ResetBit(int value, int flag)
        {
            return value &= ~(int)flag;
        }
    }
}