using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;



 public class MapLoader : Script
 {
    public MapLoader()
    {        
        API.onResourceStart += OnResourceStart;
	}

    public void OnResourceStart(object sender, EventArgs e)
    {
        var files = Directory.GetFiles("maps", "*.xml");
        int mapsLoaded = 0;
        API.consoleOutput("Loading maps...");
        foreach (var path in files)
        {
            mapsLoaded++;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var ser = new XmlSerializer(typeof(Map));
                var myMap = (Map)ser.Deserialize(stream);
                
                
                foreach (var prop in myMap.Objects)
                {
                    if (prop.Type == ObjectTypes.Prop)
                    {
                        API.createObject(prop.Hash, prop.Position, prop.Rotation);
                    }
                    else if (prop.Type == ObjectTypes.Vehicle)
                    {
                        API.createVehicle(prop.Hash, prop.Position, prop.Rotation, 0, 0);
                    }
                }
            }
        }
        
        API.consoleOutput("Loaded " + mapsLoaded + " maps!");
    }

}

public class MapObject
{
    public ObjectTypes Type;
    public Vector3 Position;
    public Vector3 Rotation;
    public int Hash;
    public bool Dynamic;

    public Quaternion Quaternion;

    // Ped stuff
    public string Action;
    public string Relationship;
    public string Weapon;
    
    // Vehicle stuff
    public bool SirensActive;

    [XmlAttribute("Id")]
    public string Id;
}

public class PedDrawables
{
    public int[] Drawables;
    public int[] Textures;
}

public enum ObjectTypes
{
    Prop,
    Vehicle,
    Ped,
    Marker,
}

public class Map
{
    public List<MapObject> Objects = new List<MapObject>();
    public List<MapObject> RemoveFromWorld = new List<MapObject>();
    public List<object> Markers = new List<object>();
}