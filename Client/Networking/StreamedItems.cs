using System.Collections.Generic;
using GTANetworkShared;

namespace GTANetwork.Networking
{
    public interface ILocalHandleable
    {
        int LocalHandle { get; set; }
    }

    public interface IStreamedItem
    {
        int RemoteHandle { get; set; }

        bool LocalOnly { get; set; }

        bool StreamedIn { get; set; }
        
        Vector3 Position { get; }

        byte EntityType { get; set; }

        int Dimension { get; set; }

        Attachment AttachedTo { get; set; }

        List<int> Attachables { get; set; }

        Movement PositionMovement { get; set; }

        Movement RotationMovement { get; set; }
    }

    public class RemotePlayer : PlayerProperties, IStreamedItem, ILocalHandleable
    {
        public int RemoteHandle { get; set; }

        public bool LocalOnly { get; set; }

        public bool StreamedIn { get; set; }

        public virtual int LocalHandle { get; set; }

        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemotePed : PedProperties, IStreamedItem, ILocalHandleable
    {
        public int RemoteHandle { get; set; }

        public bool LocalOnly { get; set; }

        public bool StreamedIn { get; set; }

        public int LocalHandle { get; set; }

        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemoteParticle : ParticleProperties, IStreamedItem, ILocalHandleable
    {
        public int RemoteHandle { get; set; }

        public bool LocalOnly { get; set; }

        public bool StreamedIn { get; set; }

        public int LocalHandle { get; set; }

        public new Vector3 Position
        {
            get
            {
                if (EntityAttached != 0)
                {
                    return Main.NetEntityHandler.NetToStreamedItem(EntityAttached)?.Position ?? new Vector3();
                }
                else
                {
                    return base.Position;
                }
            }
            set { base.Position = value; }
        }

        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemoteVehicle : VehicleProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }


        public override int GetHashCode()
        {
            return RemoteHandle;
        }

    }

    public class RemoteProp : EntityProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemoteBlip : BlipProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemoteMarker : MarkerProperties, IStreamedItem
    {
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemotePickup : PickupProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }

    public class RemoteTextLabel : TextLabelProperties, IStreamedItem
    {
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
        public override int GetHashCode()
        {
            return RemoteHandle;
        }
    }
}
