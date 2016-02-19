using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.ClearScript.Windows;

namespace GTAServer
{
    public class Resource
    {
        public string DirectoryName { get; set; }
        public ResourceInfo Info { get; set; }
        public List<JScriptEngine> Engines { get; set; }
    }

    public enum ResourceType
    {
        server,
        client
    }

    [XmlRoot("meta"), Serializable]
    public class ResourceInfo
    {
        [XmlElement("info")]
        public ResourceMetaInfo Info { get; set; }

        [XmlElement("script")]
        public List<ResourceScript> Scripts { get; set; }

        [XmlElement("file")]
        public List<FilePath> Files { get; set; }
    }

    [XmlRoot("script")]
    public class ResourceScript
    {
        [XmlAttribute("src")]
        public string Path { get; set; }
        [XmlAttribute("type")]
        public ResourceType Type { get; set; }
    }

    [XmlRoot("file")]
    public class FilePath
    {
        [XmlAttribute("src")]
        public string Path { get; set; }
    }


    [XmlRoot("info")]
    public class ResourceMetaInfo
    {
        [XmlAttribute("author")]
        public string Author { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }
    }
}