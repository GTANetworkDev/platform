using GTANetworkShared;

namespace GTANetwork
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
    }

    public class RemotePlayer : PedProperties, IStreamedItem, ILocalHandleable
    {
        public int RemoteHandle { get; set; }

        public bool LocalOnly { get; set; }

        public bool StreamedIn { get; set; }

        public int LocalHandle { get; set; }
    }

    public class RemoteVehicle : VehicleProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
    }

    public class RemoteProp : EntityProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
    }

    public class RemoteBlip : BlipProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
    }

    public class RemoteMarker : MarkerProperties, IStreamedItem
    {
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
    }

    public class RemotePickup : PickupProperties, ILocalHandleable, IStreamedItem
    {
        public int LocalHandle { get; set; }
        public int RemoteHandle { get; set; }
        public bool LocalOnly { get; set; }
        public bool StreamedIn { get; set; }
    }
}
