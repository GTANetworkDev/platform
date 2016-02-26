using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace GTANetworkServer
{
    public static class RetardedXMLParser
    {
        public static Dictionary<string, dynamic> Parse(XmlNode root)
        {
            var mainDict = new Dictionary<string, dynamic>();
            
            foreach (XmlNode node in root.ChildNodes)
            {
                if (!mainDict.ContainsKey(node.Name))
                {
                    var innerDict = new Dictionary<string, dynamic>();
                    var attribs = new Dictionary<string, string>();
                    if (node.Attributes != null)
                        foreach (XmlAttribute attrib in node.Attributes)
                        {
                            attribs.Add(attrib.Name, attrib.Value);
                        }
                    innerDict.Add("Attributes", attribs);
                    innerDict.Add("Children", Parse(node));
                    innerDict.Add("Value", node.InnerText);
                    mainDict.Add(node.Name, innerDict);
                }
                else
                {
                    var innerDict = new Dictionary<string, dynamic>();
                    var attribs = new Dictionary<string, string>();
                    if (node.Attributes != null)
                        foreach (XmlAttribute attrib in node.Attributes)
                        {
                            attribs.Add(attrib.Name, attrib.Value);
                        }
                    innerDict.Add("Attributes", attribs);
                    innerDict.Add("Children", Parse(node));
                    innerDict.Add("Value", node.InnerText);
                    
                    mainDict.Add(node.Name + mainDict.Count(x => x.Key.StartsWith(node.Name)), innerDict);
                }

            }

            return mainDict;
        }
    }
}