using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;

namespace GTANetworkServer
{
    public class XmlGroup
    {
        internal XmlDocument _mapDocument;

        internal void Load(string path)
        {
            _mapDocument = new XmlDocument();
            _mapDocument.Load(path);
        }

        internal void Load(XmlNode node)
        {
            _mapDocument = new XmlDocument();
            _mapDocument.ImportNode(node, false);
        }

        public XmlGroup getSubgroup(string groupName)
        {
            var node = _mapDocument.SelectSingleNode(groupName);
            if (node == null) return null;

            var XmlGroup = new XmlGroup();
            XmlGroup.Load(node);

            return XmlGroup;
        }

        public bool hasAnyElementOfType(string typeName)
        {
            return _mapDocument.GetElementsByTagName(typeName).Count > 0;
        }

        public int getNumberOfElementsOfType(string typeName)
        {
            return _mapDocument.GetElementsByTagName(typeName).Count;
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

        public xmlElement getElementByType(string typeName)
        {
            var nodes = _mapDocument.GetElementsByTagName(typeName);

            foreach (var node in nodes)
            {
                var xml = new xmlElement();
                xml._XmlElement = (XmlElement)node;
                xml.name = xml._XmlElement.Name;
                return xml;
            }

            return null;
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