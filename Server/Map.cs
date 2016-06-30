using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;

namespace GTANetworkServer
{
    public class Map
    {
        internal XmlDocument _mapDocument;

        internal void Load(string path)
        {
            _mapDocument = new XmlDocument();
            _mapDocument.Load(path);
        }

        public IEnumerable<xmlElement> getElementsByType(string typeName)
        {
            var nodes = _mapDocument.GetElementsByTagName(typeName);

            foreach (var node in nodes)
            {
                var xml = new xmlElement();
                xml._XmlElement = (XmlElement)node;
                xml.name = xml._XmlElement.Name;

                yield return xml;
            }
        }
    }

    public class xmlElement
    {
        internal XmlElement _XmlElement;
        public string name;

        public bool hasElementData(string elementName)
        {
            return _XmlElement.HasAttribute(elementName);
        }

        public T getElementData<T>(string elementName)
        {
            if (!_XmlElement.HasAttribute(elementName)) return default(T);
            var attribute = _XmlElement.GetAttribute(elementName);
            return (T) Convert.ChangeType(attribute, typeof (T), CultureInfo.InvariantCulture);
        }
    }
}