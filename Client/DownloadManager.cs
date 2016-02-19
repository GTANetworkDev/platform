using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using MultiTheftAutoShared;

namespace MTAV
{
    public static class DownloadManager
    {
        private static FileTransferId CurrentFile;
        public static bool StartDownload(int id, string path, FileType type, int len)
        {
            if (CurrentFile != null) return false;
            CurrentFile = new FileTransferId(id, path, type, len);
            return true;
        }

        public static void DownloadPart(int id, byte[] bytes)
        {
            if (CurrentFile == null || CurrentFile.Id != id) return;
            CurrentFile.Write(bytes);
        }

        public static void End(int id)
        {
            if (CurrentFile == null || CurrentFile.Id != id) return;

            CurrentFile.Dispose();
            if (CurrentFile.Type == FileType.Map)
            {
                var obj = Main.DeserializeBinary<ServerMap>(CurrentFile.Data.ToArray()) as ServerMap;
                if (obj == null)
                {
                    UI.Notify("ERROR DOWNLOADING MAP: NULL");
                }
                else
                {
                    Main.AddMap(obj);
                }
            }
            else if (CurrentFile.Type == FileType.Script)
            {
                var obj = Main.DeserializeBinary<ScriptCollection>(CurrentFile.Data.ToArray()) as ScriptCollection;
                if (obj == null)
                {
                    UI.Notify("ERROR DOWNLOADING SCRIPTS: NULL");
                }
                else
                {
                    Main.StartClientsideScripts(obj);
                }
            }

            CurrentFile = null;
        }
    }

    public class FileTransferId : IDisposable
    {
        public static string _DOWNLOADFOLDER_ = "scripts\\resources\\";

        public int Id { get; set; }
        public string Filename { get; set; }
        public FileType Type { get; set; }
        public FileStream Stream { get; set; }
        public int Length { get; set; }
        
        public List<byte> Data { get; set; }

        public FileTransferId(int id, string name, FileType type, int len)
        {
            Id = id;
            Filename = name;
            Type = type;
            Length = len;

            Data = new List<byte>();

            if (type == FileType.Normal && name != null)
            {
                if (!Directory.Exists(_DOWNLOADFOLDER_ + name.Replace(Path.GetFileName(name), "")))
                    Directory.CreateDirectory(_DOWNLOADFOLDER_ + name.Replace(Path.GetFileName(name), ""));
                Stream = new FileStream(_DOWNLOADFOLDER_ + name,
                    File.Exists(_DOWNLOADFOLDER_ + name) ? FileMode.Truncate : FileMode.CreateNew);
            }
        }

        public void Write(byte[] data)
        {
            if (Stream != null)
            {
                Stream.Write(data, 0, data.Length);
            }
            else
            {
                Data.AddRange(data);
            }
        }

        public void Dispose()
        {
            Stream.Close();
            Stream.Dispose();
        }
    }
}