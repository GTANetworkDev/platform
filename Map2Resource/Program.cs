using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using GTANetworkServer;

namespace Map2Resource
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.WriteLine("No input! Drag an .xml map onto the executable!");
                Console.Read();
                return;
            }

            foreach (var s in args)
            {
                ParseRace(s);
            }

            Console.Read();
        }

        static void ParseRace(string path)
        {
            var ser = new XmlSerializer(typeof(Race));

            Race race;
            try
            {
                using (var stream = File.OpenRead(path))
                    race = (Race) ser.Deserialize(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to deserialize map " + path);
                Console.WriteLine(ex);
                return;
            }

            var fname = Path.GetFileNameWithoutExtension(path);

            if (!Directory.Exists("output"))
                Directory.CreateDirectory("output");

            var dir = "race-" + fname.Replace(' ', '-');

            var totalPath = Path.Combine(Directory.GetCurrentDirectory(), "output", dir);

            Console.WriteLine("Saving map to " + totalPath);

            try
            {
                Directory.CreateDirectory(totalPath);
            }
            catch (PathTooLongException)
            {
                Console.WriteLine("Path too long!");
                Console.WriteLine(Path.GetFullPath("output" + Path.DirectorySeparatorChar + dir));
                Console.WriteLine("output" + Path.DirectorySeparatorChar + dir);
                Console.WriteLine(path);
                Console.WriteLine(totalPath);
                Console.WriteLine(fname);
                //throw;
                return;
            }

            Directory.SetCurrentDirectory(totalPath);

            string metaxml = $@"
<meta>
    <info name=""{race.Name ?? fname}"" description=""{race.Description}"" type=""map"" gamemodes=""race""/>

    <map src=""main.map"" />

</meta>";

            File.WriteAllText("meta.xml", metaxml);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var map = new StringBuilder();

            map.AppendLine("<map>");

            foreach (var checkpoint in race.Checkpoints)
            {
                map.AppendLine(string.Format("\t<checkpoint posX=\"{0}\" posY=\"{1}\" posZ=\"{2}\" />",
                    checkpoint.X, checkpoint.Y, checkpoint.Z));
            }

            map.AppendLine("");

            foreach (var checkpoint in race.SpawnPoints)
            {
                map.AppendLine(string.Format("\t<spawnpoint posX=\"{0}\" posY=\"{1}\" posZ=\"{2}\" heading=\"{3}\" />",
                    checkpoint.Position.X, checkpoint.Position.Y, checkpoint.Position.Z, checkpoint.Heading));
            }

            map.AppendLine("");

            foreach (var checkpoint in race.AvailableVehicles)
            {
                map.AppendLine(string.Format("\t<availablecar model=\"{0}\" />",
                    (int)checkpoint));
            }

            map.AppendLine("");

            foreach (var checkpoint in race.DecorativeProps)
            {
                map.AppendLine(string.Format("\t<prop posX=\"{0}\" posY=\"{1}\" posZ=\"{2}\" model=\"{3}\" rotX=\"{4}\" rotY=\"{5}\" rotZ=\"{6}\" />",
                    checkpoint.Position.X, checkpoint.Position.Y, checkpoint.Position.Z, checkpoint.Hash, checkpoint.Rotation.X, checkpoint.Rotation.Y, checkpoint.Rotation.Z));
            }

            map.AppendLine("</map>");

            File.WriteAllText("main.map", map.ToString());

            Directory.SetCurrentDirectory(".." + Path.DirectorySeparatorChar + "..");
        }
    }

    public class Vector3
    {
        public float X;
        public float Y;
        public float Z;
    }

    public class Race
    {
        public Vector3[] Checkpoints;
        public SpawnPoint[] SpawnPoints;
        public VehicleHash[] AvailableVehicles;
        public bool LapsAvailable = true;
        public Vector3 Trigger;
        public SavedProp[] DecorativeProps;

        public string Filename;

        public string Name;
        public string Description;

        public Race() { }

        public Race(Race copyFrom)
        {
            Checkpoints = copyFrom.Checkpoints;
            SpawnPoints = copyFrom.SpawnPoints;
            AvailableVehicles = copyFrom.AvailableVehicles;
            LapsAvailable = copyFrom.LapsAvailable;
            Trigger = copyFrom.Trigger;
            DecorativeProps = copyFrom.DecorativeProps;

            Name = copyFrom.Name;
            Description = copyFrom.Description;
        }
    }

    public class SpawnPoint
    {
        public Vector3 Position { get; set; }
        public float Heading { get; set; }
    }

    public class SavedProp
    {
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int Hash { get; set; }
        public bool Dynamic { get; set; }
    }
}
