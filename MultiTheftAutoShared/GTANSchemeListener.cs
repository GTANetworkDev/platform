using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace GTANetworkShared
{
    public class GTANSchemeListener : IDisposable
    {
        private MemoryMappedFile file;

        public void Create()
        {
            file = MemoryMappedFile.OpenExisting("GTANETWORKAUTOJOINSERVER", MemoryMappedFileRights.FullControl);
        }

        public void Dispose()
        {
            file.Dispose();
        }

        public string Check()
        {
            using (var accessor = file.CreateViewStream())
            using (var binReader = new BinaryReader(accessor))
            {
                if (binReader.ReadBoolean())
                {
                    byte[] ipAddr = binReader.ReadBytes(4);
                    int port = binReader.ReadInt32();

                    accessor.Position -= 9;

                    using (var str = new BinaryWriter(accessor))
                    {
                        str.Write(false);
                    }

                    return string.Format("{0}.{1}.{2}.{3}:{4}", ipAddr[0], ipAddr[1], ipAddr[2], ipAddr[3], port);
                }
            }


            return null;
        }

        public void Set(string ip)
        {
            try
            {
                string[] firstItem = ip.Split(':');
                string[] addrRaw = firstItem[0].Split('.');

                int port = int.Parse(firstItem[1]);

                byte[] addr = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    addr[i] = byte.Parse(addrRaw[i]);
                }

                using (var accessor = file.CreateViewStream())
                using (var bWriter = new BinaryWriter(accessor))
                {
                    bWriter.Write(true);
                    bWriter.Write(addr);
                    bWriter.Write(port);
                }
            }
            catch { }
        }
    }
}